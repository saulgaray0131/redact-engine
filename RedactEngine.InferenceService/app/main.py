"""
RedactEngine Inference Service

FastAPI service that wraps Grounding DINO + SAM 2 for prompt-driven video redaction.
Currently runs in mock mode — returns synthetic bounding boxes and masks for development.
When ready, real model inference can be enabled by setting INFERENCE_MODE=real.
"""

import io
import logging
import os
import subprocess
import tempfile
import uuid
from enum import Enum
from typing import Any

import cv2
import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import StreamingResponse
from PIL import Image
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

INFERENCE_MODE = os.getenv("INFERENCE_MODE", "mock")  # "mock" or "real"

app = FastAPI(
    title="RedactEngine Inference Service",
    description="Prompt-driven object detection, tracking, and redaction for video.",
    version="0.1.0",
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


class RedactionRequest(BaseModel):
    prompt: str
    redaction_style: RedactionStyle = RedactionStyle.blur
    confidence_threshold: float = 0.3


class HealthResponse(BaseModel):
    status: str
    mode: str
    models_loaded: bool


class DetectResponse(BaseModel):
    job_id: str
    prompt: str
    frame_count: int
    results: list[DetectionResult]


# ── Health ────────────────────────────────────────────────────────────────────


@app.get("/health", response_model=HealthResponse)
def health():
    return HealthResponse(
        status="healthy",
        mode=INFERENCE_MODE,
        models_loaded=INFERENCE_MODE == "mock",  # mock is always "loaded"
    )


@app.get("/alive")
def alive():
    return {"status": "alive"}


# ── Detection endpoint ───────────────────────────────────────────────────────


@app.post("/detect", response_model=DetectResponse)
async def detect(
    video: UploadFile = File(...),
    prompt: str = Form(...),
    confidence_threshold: float = Form(0.3),
):
    """
    Accept a video file and a text prompt.
    Returns bounding-box detections per sampled frame.
    """
    if not video.content_type or not video.content_type.startswith("video/"):
        raise HTTPException(status_code=400, detail="File must be a video")

    video_bytes = await video.read()
    frames = _extract_frames(video_bytes, sample_rate=10)

    if not frames:
        raise HTTPException(status_code=400, detail="Could not extract frames from video")

    results: list[DetectionResult] = []
    for i, frame in enumerate(frames):
        detections = _detect_objects(frame, prompt, confidence_threshold)
        results.append(DetectionResult(frame_index=i, detections=detections))

    return DetectResponse(
        job_id=str(uuid.uuid4()),
        prompt=prompt,
        frame_count=len(frames),
        results=results,
    )


# ── Redaction endpoint ────────────────────────────────────────────────────────


@app.post("/redact")
async def redact(
    video: UploadFile = File(...),
    prompt: str = Form(...),
    redaction_style: RedactionStyle = Form(RedactionStyle.blur),
    confidence_threshold: float = Form(0.3),
):
    """
    Accept a video file and a text prompt.
    Returns the redacted video with detected objects masked.
    """
    if not video.content_type or not video.content_type.startswith("video/"):
        raise HTTPException(status_code=400, detail="File must be a video")

    video_bytes = await video.read()
    redacted_bytes = _redact_video(video_bytes, prompt, redaction_style, confidence_threshold)

    return StreamingResponse(
        io.BytesIO(redacted_bytes),
        media_type="video/mp4",
        headers={"Content-Disposition": "attachment; filename=redacted.mp4"},
    )


# ── Internal helpers ──────────────────────────────────────────────────────────


def _extract_frames(video_bytes: bytes, sample_rate: int = 10) -> list[np.ndarray]:
    """Extract frames from video bytes at the given sample rate (every Nth frame)."""
    fd, tmp_path = tempfile.mkstemp(suffix=".mp4")
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(video_bytes)

        cap = cv2.VideoCapture(tmp_path)
        frames: list[np.ndarray] = []
        frame_idx = 0

        while cap.isOpened():
            ret, frame = cap.read()
            if not ret:
                break
            if frame_idx % sample_rate == 0:
                frames.append(frame)
            frame_idx += 1

        cap.release()
        return frames
    finally:
        if os.path.exists(tmp_path):
            os.remove(tmp_path)


def _detect_objects(
    frame: np.ndarray, prompt: str, confidence_threshold: float
) -> list[BoundingBox]:
    """
    Detect objects in a single frame matching the prompt.
    In mock mode, returns a synthetic detection in the center of the frame.
    """
    if INFERENCE_MODE == "mock":
        return _mock_detect(frame, prompt)

    # Real inference placeholder — import and call Grounding DINO here
    raise NotImplementedError("Real inference not yet implemented. Set INFERENCE_MODE=mock.")


def _mock_detect(frame: np.ndarray, prompt: str) -> list[BoundingBox]:
    """Return a synthetic bounding box roughly in the center of the frame."""
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
    video_bytes: bytes,
    prompt: str,
    style: RedactionStyle,
    confidence_threshold: float,
) -> bytes:
    """Process all frames: detect objects, apply redaction, re-encode."""
    fd_in, tmp_in = tempfile.mkstemp(suffix=".mp4")
    fd_raw, tmp_raw = tempfile.mkstemp(suffix=".avi")
    fd_out, tmp_out = tempfile.mkstemp(suffix=".mp4")
    os.close(fd_raw)
    os.close(fd_out)

    try:
        with os.fdopen(fd_in, "wb") as f:
            f.write(video_bytes)

        cap = cv2.VideoCapture(tmp_in)
        fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
        w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

        # Write frames with MJPEG into AVI — reliable across all platforms
        fourcc = cv2.VideoWriter_fourcc(*"MJPG")
        writer = cv2.VideoWriter(tmp_raw, fourcc, fps, (w, h))
        if not writer.isOpened():
            logger.error("Failed to open VideoWriter")
            raise RuntimeError("Failed to initialise video writer")

        while cap.isOpened():
            ret, frame = cap.read()
            if not ret:
                break

            detections = _detect_objects(frame, prompt, confidence_threshold)
            for det in detections:
                frame = _apply_redaction(frame, det, style)

            writer.write(frame)

        cap.release()
        writer.release()

        # Re-encode to H.264 MP4 so browsers can play the video
        result = subprocess.run(
            [
                "ffmpeg", "-y",
                "-i", tmp_raw,
                "-c:v", "libx264",
                "-preset", "fast",
                "-crf", "23",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                tmp_out,
            ],
            capture_output=True,
            timeout=300,
        )
        if result.returncode != 0:
            logger.error("ffmpeg failed: %s", result.stderr.decode())
            raise RuntimeError("ffmpeg re-encode failed")

        with open(tmp_out, "rb") as f:
            return f.read()
    finally:
        for p in (tmp_in, tmp_raw, tmp_out):
            if os.path.exists(p):
                os.remove(p)


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
