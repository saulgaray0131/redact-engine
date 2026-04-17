"""
RedactEngine Inference Service

FastAPI service that wraps Grounding DINO for prompt-driven video redaction.
Defaults to real inference using IDEA-Research/grounding-dino-tiny.
Set INFERENCE_MODE=mock to bypass model loading for CI or fast local iteration.
"""

import base64
import io
import logging
import os
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


# ── Lifespan ──────────────────────────────────────────────────────────────────


@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.processor = None
    app.state.model = None

    if INFERENCE_MODE == "real":
        import torch
        from transformers import AutoModelForZeroShotObjectDetection, AutoProcessor

        logger.info("Loading Grounding DINO model: %s", GROUNDING_DINO_MODEL)
        start = time.perf_counter()
        app.state.processor = AutoProcessor.from_pretrained(GROUNDING_DINO_MODEL)
        model = AutoModelForZeroShotObjectDetection.from_pretrained(GROUNDING_DINO_MODEL)
        model.eval()
        app.state.model = model
        app.state.torch = torch
        logger.info("Model loaded in %.2fs", time.perf_counter() - start)
    else:
        logger.info("INFERENCE_MODE=mock — skipping model load")

    yield


app = FastAPI(
    title="RedactEngine Inference Service",
    description="Prompt-driven object detection and redaction for video.",
    version="0.2.0",
    lifespan=lifespan,
)


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
    loaded = INFERENCE_MODE == "mock" or request.app.state.model is not None
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

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    image = Image.fromarray(rgb)

    inputs = processor(images=image, text=prompt, return_tensors="pt")
    with torch.no_grad():
        outputs = model(**inputs)

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
    Two-phase redaction:
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
