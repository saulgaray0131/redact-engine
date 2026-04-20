"""
RedactEngine Inference Service

FastAPI service implementing a two-stage prompt-driven video redaction pipeline:
  1. Grounding DINO localizes prompted objects on sparse anchor frames.
  2. SAM 2 propagates pixel-accurate masks across every frame in the video.

Real mode runs both models; mock mode (INFERENCE_MODE=mock) bypasses model loading
entirely and falls back to a simpler bounding-box redaction path, useful for CI or
developing the rest of the system without the ML dependencies.
"""

import base64
import bisect
import io
import logging
import os
import shutil
import subprocess
import tempfile
import time
import uuid
from contextlib import asynccontextmanager
from enum import Enum

import cv2
import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import StreamingResponse
from PIL import Image
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

INFERENCE_MODE = os.getenv("INFERENCE_MODE", "real")  # "real" or "mock"
GROUNDING_DINO_MODEL = os.getenv("GROUNDING_DINO_MODEL", "IDEA-Research/grounding-dino-tiny")
ANCHOR_INTERVAL_SECONDS = float(os.getenv("ANCHOR_INTERVAL_SECONDS", "2.0"))
MAX_ANCHORS = int(os.getenv("MAX_ANCHORS", "20"))
MAX_PREVIEW_FRAMES = int(os.getenv("MAX_PREVIEW_FRAMES", "6"))
TEXT_THRESHOLD = float(os.getenv("GROUNDING_DINO_TEXT_THRESHOLD", "0.25"))
# SAM 2 video predictor config (Hydra path inside the sam-2 package) + checkpoint on disk.
SAM2_MODEL_CONFIG = os.getenv("SAM2_MODEL_CONFIG", "configs/sam2.1/sam2.1_hiera_t.yaml")
SAM2_CHECKPOINT = os.getenv("SAM2_CHECKPOINT", "./checkpoints/sam2.1_hiera_tiny.pt")
# SAM 2's memory-attention cost grows with each propagated frame. On CPU (no
# CUDA GPU available) propagating 64+ frames takes 40+ minutes and the last
# frames spend most of their time in memory attention, not the actual segment
# work. Cap the number of frames SAM 2 sees by evenly subsampling, then reuse
# each sampled frame's mask for the original frames closest to it. Tune via
# SAM2_MAX_FRAMES; set to 0 to disable the cap.
SAM2_MAX_FRAMES = int(os.getenv("SAM2_MAX_FRAMES", "32"))
# Shared secret for cross-environment calls from the worker. When set, every
# request except /alive and /health must carry X-Inference-Key. Unset in local
# dev so the service stays open for Aspire.
INFERENCE_SERVICE_KEY = os.getenv("INFERENCE_SERVICE_KEY")


# ── Lifespan ──────────────────────────────────────────────────────────────────


@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.processor = None
    app.state.model = None
    app.state.sam2_predictor = None
    app.state.device = "cpu"
    app.state.sam2_device = "cpu"
    app.state.torch = None

    if INFERENCE_MODE == "real":
        import torch
        from transformers import AutoModelForZeroShotObjectDetection, AutoProcessor

        logger.info(
            "torch=%s cuda_build=%s cuda_available=%s device_count=%d",
            torch.__version__,
            torch.version.cuda,
            torch.cuda.is_available(),
            torch.cuda.device_count() if torch.cuda.is_available() else 0,
        )

        device = _select_device(torch)
        sam2_device = _select_sam2_device(torch)
        app.state.device = device
        app.state.sam2_device = sam2_device
        app.state.torch = torch
        logger.info("DINO device: %s; SAM 2 device: %s", device, sam2_device)

        logger.info("Loading Grounding DINO model: %s", GROUNDING_DINO_MODEL)
        start = time.perf_counter()
        app.state.processor = AutoProcessor.from_pretrained(GROUNDING_DINO_MODEL)
        model = AutoModelForZeroShotObjectDetection.from_pretrained(GROUNDING_DINO_MODEL)
        model = model.to(device)
        model.eval()
        app.state.model = model
        logger.info("Grounding DINO loaded in %.2fs", time.perf_counter() - start)

        if not os.path.exists(SAM2_CHECKPOINT):
            raise RuntimeError(
                f"SAM 2 checkpoint not found at {SAM2_CHECKPOINT}. "
                "Run RedactEngine.InferenceService/scripts/download_sam2.sh "
                "or set SAM2_CHECKPOINT to a valid path."
            )

        logger.info(
            "Loading SAM 2 video predictor (config=%s, checkpoint=%s)",
            SAM2_MODEL_CONFIG, SAM2_CHECKPOINT,
        )
        start = time.perf_counter()
        from sam2.build_sam import build_sam2_video_predictor

        app.state.sam2_predictor = build_sam2_video_predictor(
            SAM2_MODEL_CONFIG, SAM2_CHECKPOINT, device=sam2_device
        )
        logger.info("SAM 2 loaded in %.2fs", time.perf_counter() - start)
    else:
        logger.info("INFERENCE_MODE=mock — skipping model load")

    yield


app = FastAPI(
    title="RedactEngine Inference Service",
    description="Prompt-driven object detection and redaction for video.",
    version="0.2.0",
    lifespan=lifespan,
)


_PUBLIC_PATHS = {"/alive", "/health"}


@app.middleware("http")
async def _require_inference_key(request: Request, call_next):
    if INFERENCE_SERVICE_KEY and request.url.path not in _PUBLIC_PATHS:
        provided = request.headers.get("x-inference-key")
        if provided != INFERENCE_SERVICE_KEY:
            from fastapi.responses import JSONResponse
            return JSONResponse({"detail": "Unauthorized"}, status_code=401)
    return await call_next(request)


# ── Models ────────────────────────────────────────────────────────────────────


class RedactionStyle(str, Enum):
    blur = "blur"
    pixelate = "pixelate"
    fill = "fill"


class BoundingBox(BaseModel):
    x: float
    y: float
    width: float
    height: float
    confidence: float
    label: str


class DetectionResult(BaseModel):
    frame_index: int
    detections: list[BoundingBox]


class HealthResponse(BaseModel):
    status: str
    mode: str
    models_loaded: bool


class DetectionPreviewPayload(BaseModel):
    frame_index: int
    timestamp_ms: int
    image_base64: str


class DetectResponse(BaseModel):
    job_id: str
    prompt: str
    frame_count: int
    results: list[DetectionResult]
    previews: list[DetectionPreviewPayload] = []


# ── Health ────────────────────────────────────────────────────────────────────


@app.get("/health", response_model=HealthResponse)
def health(request: Request):
    if INFERENCE_MODE == "mock":
        loaded = True
    else:
        loaded = (
            request.app.state.model is not None
            and request.app.state.sam2_predictor is not None
        )
    return HealthResponse(status="healthy", mode=INFERENCE_MODE, models_loaded=loaded)


@app.get("/alive")
def alive():
    return {"status": "alive"}


# ── Detection endpoint ───────────────────────────────────────────────────────


@app.post("/detect", response_model=DetectResponse)
async def detect(
    request: Request,
    video: UploadFile = File(...),
    prompt: str = Form(...),
    confidence_threshold: float = Form(0.3),
):
    """Detect prompted objects across anchor frames of the video."""
    if not video.content_type or not video.content_type.startswith("video/"):
        raise HTTPException(status_code=400, detail="File must be a video")

    video_bytes = await video.read()
    normalized_prompt = _normalize_prompt(prompt)

    fd, tmp_path = tempfile.mkstemp(suffix=".mp4")
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(video_bytes)

        fps, anchors = _extract_anchor_frames(tmp_path)
        if not anchors:
            raise HTTPException(status_code=400, detail="Could not extract frames from video")

        results: list[DetectionResult] = []
        previews_by_frame: list[tuple[int, np.ndarray, list[BoundingBox]]] = []
        for frame_idx, frame in anchors:
            detections = _detect_objects(
                request.app, frame, normalized_prompt, confidence_threshold
            )
            results.append(DetectionResult(frame_index=frame_idx, detections=detections))
            if detections:
                previews_by_frame.append((frame_idx, frame, detections))

        selected = _subsample_evenly(previews_by_frame, MAX_PREVIEW_FRAMES)
        previews: list[DetectionPreviewPayload] = []
        for frame_idx, frame, detections in selected:
            try:
                jpeg = _render_preview(frame, detections)
                previews.append(
                    DetectionPreviewPayload(
                        frame_index=frame_idx,
                        timestamp_ms=int(frame_idx / fps * 1000),
                        image_base64=base64.b64encode(jpeg).decode("ascii"),
                    )
                )
            except Exception:
                logger.exception("Failed to render preview for frame %d", frame_idx)

        return DetectResponse(
            job_id=str(uuid.uuid4()),
            prompt=prompt,
            frame_count=len(anchors),
            results=results,
            previews=previews,
        )
    finally:
        if os.path.exists(tmp_path):
            os.remove(tmp_path)


# ── Redaction endpoint ────────────────────────────────────────────────────────


@app.post("/redact")
async def redact(
    request: Request,
    video: UploadFile = File(...),
    prompt: str = Form(...),
    redaction_style: RedactionStyle = Form(RedactionStyle.blur),
    confidence_threshold: float = Form(0.3),
):
    """Detect on anchor frames, union detections, apply redaction to every frame."""
    if not video.content_type or not video.content_type.startswith("video/"):
        raise HTTPException(status_code=400, detail="File must be a video")

    video_bytes = await video.read()
    redacted_bytes = _redact_video(
        request.app, video_bytes, prompt, redaction_style, confidence_threshold
    )

    return StreamingResponse(
        io.BytesIO(redacted_bytes),
        media_type="video/mp4",
        headers={"Content-Disposition": "attachment; filename=redacted.mp4"},
    )


# ── Internal helpers ──────────────────────────────────────────────────────────


def _normalize_prompt(prompt: str) -> str:
    """Grounding DINO expects lowercase, period-separated phrases."""
    normalized = prompt.strip().lower()
    if not normalized.endswith("."):
        normalized += "."
    return normalized


def _extract_anchor_frames(video_path: str) -> tuple[float, list[tuple[int, np.ndarray]]]:
    """
    Pick anchor frames evenly spaced across the video.

    Count = min(MAX_ANCHORS, max(1, ceil(duration_s / ANCHOR_INTERVAL_SECONDS))).
    Returns (fps, [(real_frame_index, bgr_frame), ...]).
    """
    cap = cv2.VideoCapture(video_path)
    try:
        fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
        total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
        if total_frames <= 0:
            return fps, []

        duration = total_frames / fps
        anchor_count = max(1, int(np.ceil(duration / ANCHOR_INTERVAL_SECONDS)))
        anchor_count = min(anchor_count, MAX_ANCHORS, total_frames)

        # Evenly space anchors; for anchor_count=1 use frame 0, else distribute across video.
        if anchor_count == 1:
            indices = [0]
        else:
            indices = [
                int(round(i * (total_frames - 1) / (anchor_count - 1)))
                for i in range(anchor_count)
            ]

        frames: list[tuple[int, np.ndarray]] = []
        for idx in indices:
            cap.set(cv2.CAP_PROP_POS_FRAMES, idx)
            ret, frame = cap.read()
            if ret:
                frames.append((idx, frame))

        return fps, frames
    finally:
        cap.release()


def _subsample_evenly(items: list, cap: int) -> list:
    """Return up to `cap` evenly-spaced items from the input list."""
    if len(items) <= cap:
        return items
    if cap <= 1:
        return items[:1]
    return [items[round(i * (len(items) - 1) / (cap - 1))] for i in range(cap)]


def _render_preview(frame: np.ndarray, detections: list[BoundingBox]) -> bytes:
    """Render bounding boxes on a copy of the frame, return JPEG bytes."""
    canvas = frame.copy()
    green = (0, 255, 0)
    black = (0, 0, 0)
    white = (255, 255, 255)
    font = cv2.FONT_HERSHEY_SIMPLEX
    font_scale = 0.5
    font_thickness = 1

    for det in detections:
        x1 = max(0, int(det.x))
        y1 = max(0, int(det.y))
        x2 = int(det.x + det.width)
        y2 = int(det.y + det.height)
        cv2.rectangle(canvas, (x1, y1), (x2, y2), green, 3)

        label = f"{det.label} {int(det.confidence * 100)}%"
        (tw, th), baseline = cv2.getTextSize(label, font, font_scale, font_thickness)
        # Backdrop above the bbox (or inside, if near the top edge).
        strip_y1 = max(0, y1 - th - baseline - 4)
        strip_y2 = strip_y1 + th + baseline + 4
        cv2.rectangle(canvas, (x1, strip_y1), (x1 + tw + 6, strip_y2), black, -1)
        cv2.putText(
            canvas,
            label,
            (x1 + 3, strip_y2 - baseline),
            font,
            font_scale,
            white,
            font_thickness,
            cv2.LINE_AA,
        )

    ok, buf = cv2.imencode(".jpg", canvas, [cv2.IMWRITE_JPEG_QUALITY, 80])
    if not ok:
        raise RuntimeError("Failed to JPEG-encode preview frame")
    return buf.tobytes()


def _detect_objects(
    app: FastAPI, frame: np.ndarray, prompt: str, confidence_threshold: float
) -> list[BoundingBox]:
    """Detect objects in a frame matching the prompt."""
    if INFERENCE_MODE == "mock":
        return _mock_detect(frame, prompt)

    if app.state.model is None:
        raise HTTPException(status_code=503, detail="Model is still loading")

    return _run_grounding_dino(app, frame, prompt, confidence_threshold)


def _run_grounding_dino(
    app: FastAPI, frame: np.ndarray, prompt: str, box_threshold: float
) -> list[BoundingBox]:
    """Run Grounding DINO on a single BGR frame, return boxes in xywh."""
    torch = app.state.torch
    processor = app.state.processor
    model = app.state.model
    device = app.state.device

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    image = Image.fromarray(rgb)

    # Inputs must live on the same device as the model weights, otherwise the
    # embedding layer errors with "Placeholder storage has not been allocated on
    # MPS device!" (or the CUDA equivalent). `.to(device)` on a BatchFeature
    # moves every contained tensor in one shot.
    inputs = processor(images=image, text=prompt, return_tensors="pt").to(device)
    with torch.no_grad():
        outputs = model(**inputs)

    # post_process expects CPU tensors; target_sizes is a CPU tensor by construction.
    target_sizes = torch.tensor([image.size[::-1]])  # (H, W)
    results = processor.post_process_grounded_object_detection(
        outputs,
        inputs.input_ids,
        box_threshold=box_threshold,
        text_threshold=TEXT_THRESHOLD,
        target_sizes=target_sizes,
    )[0]

    boxes: list[BoundingBox] = []
    for box, score, label in zip(results["boxes"], results["scores"], results["labels"]):
        x1, y1, x2, y2 = box.tolist()
        width = max(0.0, x2 - x1)
        height = max(0.0, y2 - y1)
        if width <= 0 or height <= 0:
            continue
        label_str = label if isinstance(label, str) else str(label)
        boxes.append(
            BoundingBox(
                x=float(x1),
                y=float(y1),
                width=float(width),
                height=float(height),
                confidence=round(float(score), 3),
                label=label_str or prompt,
            )
        )

    return boxes


def _mock_detect(frame: np.ndarray, prompt: str) -> list[BoundingBox]:
    """Return a synthetic bounding box near the center of the frame."""
    h, w = frame.shape[:2]
    box_w = w * 0.15
    box_h = h * 0.15
    cx = w * 0.5 + np.random.uniform(-w * 0.1, w * 0.1)
    cy = h * 0.5 + np.random.uniform(-h * 0.1, h * 0.1)

    return [
        BoundingBox(
            x=cx - box_w / 2,
            y=cy - box_h / 2,
            width=box_w,
            height=box_h,
            confidence=round(float(np.random.uniform(0.7, 0.95)), 3),
            label=prompt,
        )
    ]


def _redact_video(
    app: FastAPI,
    video_bytes: bytes,
    prompt: str,
    style: RedactionStyle,
    confidence_threshold: float,
) -> bytes:
    """
    Dispatch to SAM 2 mask-based redaction in real mode, falling back to the
    box-based path in mock mode (or when SAM 2 failed to load for any reason).
    """
    if INFERENCE_MODE == "real" and app.state.sam2_predictor is not None:
        return _redact_video_sam2(app, video_bytes, prompt, style, confidence_threshold)
    return _redact_video_boxes(app, video_bytes, prompt, style, confidence_threshold)


def _redact_video_boxes(
    app: FastAPI,
    video_bytes: bytes,
    prompt: str,
    style: RedactionStyle,
    confidence_threshold: float,
) -> bytes:
    """
    Box-based two-phase redaction (mock-mode path, no SAM 2):
    1. Extract anchor frames, run detection on each, union detections.
    2. Apply all unioned detections to every frame, re-encode via ffmpeg.
    """
    fd_in, tmp_in = tempfile.mkstemp(suffix=".mp4")
    fd_out, tmp_out = tempfile.mkstemp(suffix=".mp4")
    os.close(fd_out)

    cap = None
    process = None

    try:
        with os.fdopen(fd_in, "wb") as f:
            f.write(video_bytes)

        normalized_prompt = _normalize_prompt(prompt)
        _, anchors = _extract_anchor_frames(tmp_in)

        unioned: list[BoundingBox] = []
        for _, frame in anchors:
            unioned.extend(
                _detect_objects(app, frame, normalized_prompt, confidence_threshold)
            )
        logger.info(
            "Redact: %d anchors, %d unioned detections, style=%s",
            len(anchors), len(unioned), style.value,
        )

        cap = cv2.VideoCapture(tmp_in)
        fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
        w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

        # Pipe raw frames directly into ffmpeg — avoids large intermediate files
        # that would OOM the container.
        process = subprocess.Popen(
            [
                "ffmpeg", "-y",
                "-f", "rawvideo",
                "-pix_fmt", "bgr24",
                "-s", f"{w}x{h}",
                "-r", str(fps),
                "-i", "pipe:0",
                "-c:v", "libx264",
                "-preset", "fast",
                "-crf", "23",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                tmp_out,
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )

        while cap.isOpened():
            ret, frame = cap.read()
            if not ret:
                break

            for det in unioned:
                frame = _apply_redaction(frame, det, style)

            process.stdin.write(frame.tobytes())

        cap.release()
        cap = None
        _, stderr = process.communicate(timeout=300)

        if process.returncode != 0:
            logger.error("ffmpeg failed: %s", stderr.decode())
            raise RuntimeError("ffmpeg re-encode failed")

        with open(tmp_out, "rb") as f:
            return f.read()
    finally:
        if cap is not None:
            cap.release()
        if process is not None and process.poll() is None:
            process.kill()
        for p in (tmp_in, tmp_out):
            try:
                os.remove(p)
            except OSError:
                pass


def _apply_redaction(
    frame: np.ndarray, box: BoundingBox, style: RedactionStyle
) -> np.ndarray:
    """Apply redaction to a region of the frame."""
    h, w = frame.shape[:2]
    x1 = max(0, int(box.x))
    y1 = max(0, int(box.y))
    x2 = min(w, int(box.x + box.width))
    y2 = min(h, int(box.y + box.height))

    if x2 <= x1 or y2 <= y1:
        return frame

    roi = frame[y1:y2, x1:x2]

    if style == RedactionStyle.blur:
        ksize = max(15, (x2 - x1) // 3 | 1)  # ensure odd kernel
        frame[y1:y2, x1:x2] = cv2.GaussianBlur(roi, (ksize, ksize), 30)
    elif style == RedactionStyle.pixelate:
        pixel_size = max(2, min(x2 - x1, y2 - y1) // 8)
        small = cv2.resize(roi, (pixel_size, pixel_size), interpolation=cv2.INTER_NEAREST)
        frame[y1:y2, x1:x2] = cv2.resize(small, (x2 - x1, y2 - y1), interpolation=cv2.INTER_NEAREST)
    elif style == RedactionStyle.fill:
        frame[y1:y2, x1:x2] = (0, 0, 0)

    return frame


# ── SAM 2 video tracking pipeline ─────────────────────────────────────────────


def _select_device(torch) -> str:
    """Pick the best available inference device for Grounding DINO: CUDA > MPS > CPU."""
    if torch.cuda.is_available():
        return "cuda"
    if getattr(torch.backends, "mps", None) is not None and torch.backends.mps.is_available():
        return "mps"
    return "cpu"


def _select_sam2_device(torch) -> str:
    """
    Pick SAM 2's device. Unlike DINO, SAM 2 has known MPS incompatibilities on
    Apple Silicon (bfloat16 matmul kernels fail Apple's dtype assertion), so we
    deliberately skip MPS and fall back to CPU instead. Override with the
    SAM2_DEVICE env var if you want to force-try MPS with
    PYTORCH_ENABLE_MPS_FALLBACK=1, or run on CUDA from a dedicated GPU host.
    """
    override = os.getenv("SAM2_DEVICE")
    if override:
        return override
    if torch.cuda.is_available():
        return "cuda"
    return "cpu"


def _extract_all_frames(video_path: str, frames_dir: str) -> tuple[float, int, int, int]:
    """
    Extract every frame of the video as zero-padded JPEGs into `frames_dir`.

    SAM 2's video predictor expects a directory of JPEGs named "00000.jpg",
    "00001.jpg", ... — which is also how ffmpeg numbers its output by default.

    Returns (fps, width, height, frame_count).
    """
    # Only pull fps from the container probe — it's not affected by rotation.
    cap = cv2.VideoCapture(video_path)
    try:
        fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
    finally:
        cap.release()

    # Use ffmpeg for frame extraction — it's faster and more reliable than
    # OpenCV's per-frame seek, especially for variable-framerate input.
    os.makedirs(frames_dir, exist_ok=True)
    result = subprocess.run(
        [
            "ffmpeg", "-y", "-loglevel", "error",
            "-i", video_path,
            "-q:v", "2",
            "-start_number", "0",
            os.path.join(frames_dir, "%05d.jpg"),
        ],
        capture_output=True,
    )
    if result.returncode != 0:
        raise RuntimeError(f"ffmpeg frame extraction failed: {result.stderr.decode()}")

    # Derive width/height from the actual extracted JPEG, not the MP4 container.
    # ffmpeg auto-rotates videos with rotation metadata (iPhone portrait, etc.)
    # while OpenCV's CAP_PROP_FRAME_WIDTH/HEIGHT reports pre-rotation dims — a
    # mismatch between those two is what caused the raw-frame pipe downstream
    # to interpret each row at the wrong stride, producing horizontal banding.
    frame_files = sorted(
        f for f in os.listdir(frames_dir) if f.endswith(".jpg")
    )
    frame_count = len(frame_files)
    width = height = 0
    if frame_count > 0:
        first = cv2.imread(os.path.join(frames_dir, frame_files[0]))
        if first is not None:
            height, width = first.shape[:2]
    return fps, width, height, frame_count


def _apply_redaction_mask(
    frame: np.ndarray, mask: np.ndarray, style: RedactionStyle
) -> np.ndarray:
    """
    Apply the given redaction style to pixels of `frame` where `mask` is True.

    Operates on the tight bounding box of the mask for efficiency, then composites
    the redacted pixels back under the boolean mask so the effect follows the
    object's silhouette rather than its bounding box.
    """
    if mask is None or not mask.any():
        return frame

    h, w = frame.shape[:2]
    if mask.shape != (h, w):
        # Defensive: resize mask to frame dims if a shape mismatch slipped through.
        mask = cv2.resize(
            mask.astype(np.uint8), (w, h), interpolation=cv2.INTER_NEAREST
        ).astype(bool)

    ys, xs = np.where(mask)
    y1, y2 = int(ys.min()), int(ys.max()) + 1
    x1, x2 = int(xs.min()), int(xs.max()) + 1

    roi = frame[y1:y2, x1:x2]
    roi_mask = mask[y1:y2, x1:x2]

    if style == RedactionStyle.blur:
        ksize = max(15, min(x2 - x1, y2 - y1) // 3 | 1)
        redacted = cv2.GaussianBlur(roi, (ksize, ksize), 30)
    elif style == RedactionStyle.pixelate:
        pixel_size = max(2, min(x2 - x1, y2 - y1) // 8)
        small = cv2.resize(roi, (pixel_size, pixel_size), interpolation=cv2.INTER_NEAREST)
        redacted = cv2.resize(small, (x2 - x1, y2 - y1), interpolation=cv2.INTER_NEAREST)
    elif style == RedactionStyle.fill:
        redacted = np.zeros_like(roi)
    else:
        return frame

    roi[roi_mask] = redacted[roi_mask]
    frame[y1:y2, x1:x2] = roi
    return frame


def _redact_video_sam2(
    app: FastAPI,
    video_bytes: bytes,
    prompt: str,
    style: RedactionStyle,
    confidence_threshold: float,
) -> bytes:
    """
    Real-mode redaction pipeline:
      1. Dump every frame to a temp directory as JPEGs.
      2. Run Grounding DINO on sparse anchor frames to get seed bounding boxes.
      3. Feed the boxes to SAM 2's video predictor as per-object prompts,
         then propagate pixel-accurate masks across the full clip.
      4. Re-read frames, composite the style-appropriate redaction under each
         tracked mask, pipe the result back into ffmpeg for re-encoding.
    """
    torch = app.state.torch
    predictor = app.state.sam2_predictor
    sam2_device = app.state.sam2_device

    work_dir = tempfile.mkdtemp(prefix="redact_sam2_")
    tmp_in = os.path.join(work_dir, "input.mp4")
    frames_dir = os.path.join(work_dir, "frames")
    tmp_out = os.path.join(work_dir, "output.mp4")

    process = None
    try:
        with open(tmp_in, "wb") as f:
            f.write(video_bytes)

        fps, width, height, frame_count = _extract_all_frames(tmp_in, frames_dir)
        if frame_count == 0:
            raise HTTPException(status_code=400, detail="Could not extract frames from video")

        normalized_prompt = _normalize_prompt(prompt)

        # Step 1: seed detections on anchor frames via Grounding DINO.
        _, anchors = _extract_anchor_frames(tmp_in)
        seeds: list[tuple[int, BoundingBox]] = []  # (frame_index, box)
        for frame_idx, frame in anchors:
            detections = _run_grounding_dino(
                app, frame, normalized_prompt, confidence_threshold
            )
            for det in detections:
                seeds.append((frame_idx, det))

        logger.info(
            "SAM2 redact: %d frames, %d anchors, %d seed detections, style=%s",
            frame_count, len(anchors), len(seeds), style.value,
        )

        # If DINO found nothing to track, skip SAM 2 entirely and return the
        # original video re-encoded — nothing to redact.
        if not seeds:
            return _reencode_from_frames(frames_dir, tmp_out, fps)

        # Build a (possibly subsampled) frame directory for SAM 2. Propagation
        # cost grows with each added frame, so we cap the number of frames SAM 2
        # actually sees at SAM2_MAX_FRAMES; each original frame is then mapped
        # back to its nearest sampled-frame mask for compositing.
        if 0 < SAM2_MAX_FRAMES < frame_count:
            if SAM2_MAX_FRAMES > 1:
                step = (frame_count - 1) / (SAM2_MAX_FRAMES - 1)
                sampled_orig = sorted({
                    int(round(i * step)) for i in range(SAM2_MAX_FRAMES)
                })
            else:
                sampled_orig = [0]
            sampled_dir = os.path.join(work_dir, "sampled")
            os.makedirs(sampled_dir, exist_ok=True)
            for s_idx, orig_idx in enumerate(sampled_orig):
                src = os.path.join(frames_dir, f"{orig_idx:05d}.jpg")
                dst = os.path.join(sampled_dir, f"{s_idx:05d}.jpg")
                # Hardlinks avoid copying bytes and work everywhere on the same
                # filesystem; fall back to a plain copy if the FS refuses them.
                try:
                    os.link(src, dst)
                except OSError:
                    shutil.copy(src, dst)
            frames_to_use_dir = sampled_dir
            logger.info(
                "SAM2 subsample: processing %d/%d frames (cap=%d)",
                len(sampled_orig), frame_count, SAM2_MAX_FRAMES,
            )
        else:
            sampled_orig = list(range(frame_count))
            frames_to_use_dir = frames_dir

        def nearest_sampled_idx(orig_idx: int) -> int:
            """Index into sampled_orig whose original frame is closest to orig_idx."""
            i = bisect.bisect_left(sampled_orig, orig_idx)
            if i == 0:
                return 0
            if i >= len(sampled_orig):
                return len(sampled_orig) - 1
            before = sampled_orig[i - 1]
            after = sampled_orig[i]
            return i if (after - orig_idx) < (orig_idx - before) else i - 1

        # SAM 2's image encoder is hardcoded to bfloat16, while the memory
        # attention layers have float32 weights — without autocast this mismatches
        # on CPU ("mat1 BFloat16 and mat2 Float"). Autocast resolves the dtype
        # per-op, so enable it on both cuda and cpu. MPS autocast bf16 triggers
        # its own Metal kernel assertion, so we skip it there (non-issue because
        # SAM 2 itself runs on CPU on Apple Silicon by default).
        # Wrap ALL predictor calls (init_state, add_new_points_or_box, and
        # propagate_in_video) so the image encoder runs under autocast too.
        autocast_ctx = (
            torch.autocast(device_type=sam2_device, dtype=torch.bfloat16)
            if sam2_device in ("cuda", "cpu")
            else _nullcontext()
        )
        # masks_by_sampled keys are indices into sampled_orig (0..len-1), not
        # original frame numbers. We expand to original-frame keys afterwards.
        masks_by_sampled: dict[int, np.ndarray] = {}
        with torch.inference_mode(), autocast_ctx:
            # Step 2: register each seed as a new object in the predictor's
            # state and propagate masks forward. Seeds are produced in
            # original-frame coordinates; remap them to the sampled frame
            # closest to the anchor so the predictor finds them.
            inference_state = predictor.init_state(video_path=frames_to_use_dir)

            for obj_id, (orig_frame_idx, box) in enumerate(seeds):
                x1 = float(box.x)
                y1 = float(box.y)
                x2 = float(box.x + box.width)
                y2 = float(box.y + box.height)
                predictor.add_new_points_or_box(
                    inference_state=inference_state,
                    frame_idx=nearest_sampled_idx(orig_frame_idx),
                    obj_id=obj_id,
                    box=np.array([x1, y1, x2, y2], dtype=np.float32),
                )

            # Accumulate a per-frame union-mask across all tracked objects.
            for s_idx, _obj_ids, mask_logits in predictor.propagate_in_video(
                inference_state
            ):
                # mask_logits shape: (num_objects, 1, H, W)
                bool_masks = (mask_logits > 0.0).squeeze(1).cpu().numpy()
                union = np.any(bool_masks, axis=0) if bool_masks.size else None
                if union is not None and union.any():
                    masks_by_sampled[s_idx] = union

        # Expand sampled masks back to the full frame range via nearest-sampled
        # assignment. No subsampling → identity map, so short clips behave the
        # same as before this change.
        masks_by_frame: dict[int, np.ndarray] = {}
        for orig_idx in range(frame_count):
            s_idx = nearest_sampled_idx(orig_idx)
            mask = masks_by_sampled.get(s_idx)
            if mask is not None:
                masks_by_frame[orig_idx] = mask

        logger.info(
            "SAM2: masks for %d/%d sampled frames, expanded to %d original frames",
            len(masks_by_sampled), len(sampled_orig), len(masks_by_frame),
        )

        # Step 3: composite redactions and re-encode via ffmpeg piping raw frames.
        process = subprocess.Popen(
            [
                "ffmpeg", "-y", "-loglevel", "error",
                "-f", "rawvideo",
                "-pix_fmt", "bgr24",
                "-s", f"{width}x{height}",
                "-r", str(fps),
                "-i", "pipe:0",
                "-c:v", "libx264",
                "-preset", "fast",
                "-crf", "23",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                tmp_out,
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )

        for frame_idx in range(frame_count):
            frame_path = os.path.join(frames_dir, f"{frame_idx:05d}.jpg")
            frame = cv2.imread(frame_path)
            if frame is None:
                continue
            mask = masks_by_frame.get(frame_idx)
            if mask is not None:
                frame = _apply_redaction_mask(frame, mask, style)
            process.stdin.write(frame.tobytes())

        # Don't close stdin manually — communicate() flushes and closes it itself,
        # and an explicit close here would cause `ValueError: flush of closed file`.
        _, stderr = process.communicate(timeout=600)
        if process.returncode != 0:
            logger.error("ffmpeg re-encode failed: %s", stderr.decode())
            raise RuntimeError("ffmpeg re-encode failed")

        with open(tmp_out, "rb") as f:
            return f.read()
    finally:
        if process is not None and process.poll() is None:
            process.kill()
        # Best-effort cleanup of the temp working directory.
        shutil.rmtree(work_dir, ignore_errors=True)


def _reencode_from_frames(frames_dir: str, output_path: str, fps: float) -> bytes:
    """Re-encode a directory of JPEG frames into an mp4 with no modifications."""
    result = subprocess.run(
        [
            "ffmpeg", "-y", "-loglevel", "error",
            "-framerate", str(fps),
            "-i", os.path.join(frames_dir, "%05d.jpg"),
            "-c:v", "libx264",
            "-preset", "fast",
            "-crf", "23",
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
            output_path,
        ],
        capture_output=True,
    )
    if result.returncode != 0:
        raise RuntimeError(f"ffmpeg re-encode failed: {result.stderr.decode()}")
    with open(output_path, "rb") as f:
        return f.read()


def _nullcontext():
    """Tiny replacement for contextlib.nullcontext to avoid another import."""
    from contextlib import nullcontext
    return nullcontext()
