"""
Thin client for the deployed RedactEngine.InferenceService.

The eval scripts use the synchronous /detect endpoint only — /redact is
asynchronous (uploads to blob, posts a callback) and isn't useful for measuring
detection / tracking quality directly. /detect runs Grounding DINO against a
configurable number of "anchor" frames and returns bounding boxes per anchor;
that's what we score against LVIS / DAVIS ground truth.

The deployed service requires the X-Inference-Key header when configured (see
RedactEngine.InferenceService/app/main.py:_require_inference_key).
"""

from __future__ import annotations

import io
import os
import subprocess
import tempfile
import time
from dataclasses import dataclass
from typing import Iterable

import cv2
import httpx
import numpy as np


# Tune up the timeout — the deployed inference container can spend tens of
# seconds in DINO on its first request after a cold start. The eval scripts
# pipeline a lot of requests, so we don't want to die on the first slow one.
DEFAULT_TIMEOUT_S = float(os.getenv("INFERENCE_HTTP_TIMEOUT", "180"))
DEFAULT_RETRIES = int(os.getenv("INFERENCE_HTTP_RETRIES", "3"))
DEFAULT_BACKOFF_S = float(os.getenv("INFERENCE_HTTP_BACKOFF", "2.0"))


@dataclass
class BoundingBox:
    """Mirrors the inference service's BoundingBox model."""

    x: float
    y: float
    width: float
    height: float
    confidence: float
    label: str

    @property
    def xyxy(self) -> tuple[float, float, float, float]:
        return self.x, self.y, self.x + self.width, self.y + self.height

    @property
    def xywh(self) -> tuple[float, float, float, float]:
        return self.x, self.y, self.width, self.height


@dataclass
class FrameDetections:
    frame_index: int
    detections: list[BoundingBox]


@dataclass
class DetectResponse:
    job_id: str
    prompt: str
    frame_count: int
    results: list[FrameDetections]


@dataclass
class TrackedFrame:
    frame_index: int
    # Already decoded to a uint8 binary mask {0, 1} of shape (H, W).
    mask: "np.ndarray"


@dataclass
class TrackResponse:
    job_id: str
    prompt: str
    frame_count: int
    width: int
    height: int
    masks: list[TrackedFrame]


class InferenceClient:
    """
    HTTP client wrapping POST /detect.

    Construct one client per script run; it owns the underlying httpx session
    so connection pooling kicks in across the typically-thousands of LVIS or
    DAVIS calls a full eval makes.
    """

    def __init__(
        self,
        base_url: str | None = None,
        api_key: str | None = None,
        timeout_s: float = DEFAULT_TIMEOUT_S,
        retries: int = DEFAULT_RETRIES,
    ) -> None:
        self.base_url = (base_url or os.environ["INFERENCE_BASE_URL"]).rstrip("/")
        self.api_key = api_key or os.getenv("INFERENCE_SERVICE_KEY")
        self.retries = retries
        headers = {}
        if self.api_key:
            headers["X-Inference-Key"] = self.api_key
        self._client = httpx.Client(
            base_url=self.base_url,
            timeout=timeout_s,
            headers=headers,
            follow_redirects=True,
        )

    def close(self) -> None:
        self._client.close()

    def __enter__(self) -> "InferenceClient":
        return self

    def __exit__(self, *_exc) -> None:
        self.close()

    # ── HTTP ──────────────────────────────────────────────────────────────────

    def detect(
        self,
        video_bytes: bytes,
        prompt: str,
        confidence_threshold: float = 0.3,
        filename: str = "clip.mp4",
    ) -> DetectResponse:
        """POST /detect with the given mp4 bytes; raise on non-2xx."""

        files = {"video": (filename, video_bytes, "video/mp4")}
        data = {"prompt": prompt, "confidence_threshold": str(confidence_threshold)}

        last_exc: Exception | None = None
        for attempt in range(self.retries):
            try:
                resp = self._client.post("/detect", files=files, data=data)
                resp.raise_for_status()
                payload = resp.json()
                return _parse_detect_response(payload)
            except (httpx.HTTPError, httpx.HTTPStatusError) as exc:
                last_exc = exc
                if attempt + 1 == self.retries:
                    break
                time.sleep(DEFAULT_BACKOFF_S * (2**attempt))
        raise RuntimeError(
            f"/detect failed after {self.retries} attempts: {last_exc}"
        )

    def track(
        self,
        video_bytes: bytes,
        prompt: str,
        confidence_threshold: float = 0.3,
        filename: str = "clip.mp4",
    ) -> TrackResponse:
        """
        POST /track and decode the returned per-frame PNG masks.

        Returns a TrackResponse whose `masks` field contains decoded
        uint8 {0, 1} arrays of shape (height, width). Frames the server
        omitted from the response (no SAM 2 mask) are NOT filled in here
        — callers should default to all-zero of (height, width).
        """
        import base64

        files = {"video": (filename, video_bytes, "video/mp4")}
        data = {"prompt": prompt, "confidence_threshold": str(confidence_threshold)}

        last_exc: Exception | None = None
        for attempt in range(self.retries):
            try:
                resp = self._client.post("/track", files=files, data=data)
                resp.raise_for_status()
                payload = resp.json()
                return _parse_track_response(payload, base64)
            except (httpx.HTTPError, httpx.HTTPStatusError) as exc:
                last_exc = exc
                if attempt + 1 == self.retries:
                    break
                time.sleep(DEFAULT_BACKOFF_S * (2**attempt))
        raise RuntimeError(
            f"/track failed after {self.retries} attempts: {last_exc}"
        )


def _parse_detect_response(payload: dict) -> DetectResponse:
    results = []
    for r in payload.get("results", []):
        boxes = [
            BoundingBox(
                x=float(b["x"]),
                y=float(b["y"]),
                width=float(b["width"]),
                height=float(b["height"]),
                confidence=float(b["confidence"]),
                label=str(b.get("label", "")),
            )
            for b in r.get("detections", [])
        ]
        results.append(
            FrameDetections(frame_index=int(r["frame_index"]), detections=boxes)
        )
    return DetectResponse(
        job_id=str(payload.get("job_id", "")),
        prompt=str(payload.get("prompt", "")),
        frame_count=int(payload.get("frame_count", 0)),
        results=results,
    )


def _parse_track_response(payload: dict, base64_mod) -> TrackResponse:
    masks: list[TrackedFrame] = []
    for entry in payload.get("masks", []):
        png_bytes = base64_mod.b64decode(entry["png_base64"])
        decoded = cv2.imdecode(
            np.frombuffer(png_bytes, dtype=np.uint8), cv2.IMREAD_GRAYSCALE,
        )
        if decoded is None:
            continue
        masks.append(
            TrackedFrame(
                frame_index=int(entry["frame_index"]),
                mask=(decoded > 127).astype(np.uint8),
            )
        )
    return TrackResponse(
        job_id=str(payload.get("job_id", "")),
        prompt=str(payload.get("prompt", "")),
        frame_count=int(payload.get("frame_count", 0)),
        width=int(payload.get("width", 0)),
        height=int(payload.get("height", 0)),
        masks=masks,
    )


# ── Helpers: pack image / image-sequence into mp4 bytes ──────────────────────


def encode_single_image_as_mp4(image: np.ndarray) -> bytes:
    """
    Wrap a single BGR image as a 1-second 1-frame mp4.

    Used by the LVIS script: the inference service only accepts videos, but
    LVIS is image-only, so we wrap each image as the smallest possible mp4
    that the service's anchor-frame extractor will return on the first frame.
    """

    if image is None or image.size == 0:
        raise ValueError("encode_single_image_as_mp4: empty image")
    if image.ndim != 3 or image.shape[2] != 3:
        raise ValueError(
            f"encode_single_image_as_mp4: expected HxWx3 BGR, got {image.shape}"
        )

    height, width = image.shape[:2]
    with tempfile.TemporaryDirectory() as work:
        in_path = os.path.join(work, "frame.png")
        out_path = os.path.join(work, "out.mp4")
        # libx264 needs even dimensions; if either is odd, pad by 1.
        cv2.imwrite(in_path, image)
        proc = subprocess.run(
            [
                "ffmpeg", "-y", "-loglevel", "error",
                "-loop", "1",
                "-framerate", "1",
                "-t", "1",
                "-i", in_path,
                "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                out_path,
            ],
            check=False,
            capture_output=True,
        )
        if proc.returncode != 0:
            raise RuntimeError(
                f"ffmpeg image→mp4 failed: {proc.stderr.decode()[:500]}"
            )
        with open(out_path, "rb") as f:
            return f.read()


def encode_image_sequence_as_mp4(
    frame_paths: Iterable[str],
    fps: float = 24.0,
) -> tuple[bytes, int, int, int]:
    """
    Concatenate a sequence of image files into an mp4.

    Returns (mp4_bytes, frame_count, width, height). Used by the DAVIS script
    to send each sequence as a single video to /detect.
    """

    paths = list(frame_paths)
    if not paths:
        raise ValueError("encode_image_sequence_as_mp4: empty sequence")

    first = cv2.imread(paths[0])
    if first is None:
        raise RuntimeError(f"Could not read {paths[0]}")
    height, width = first.shape[:2]

    with tempfile.TemporaryDirectory() as work:
        # Symlink frames into a 5-digit-padded directory so ffmpeg's image2
        # demuxer can ingest them with a sequential pattern. Fall back to
        # copying if the FS rejects symlinks.
        for i, src in enumerate(paths):
            dst = os.path.join(work, f"{i:05d}.png")
            try:
                os.symlink(os.path.abspath(src), dst)
            except OSError:
                with open(src, "rb") as r, open(dst, "wb") as w:
                    w.write(r.read())

        out_path = os.path.join(work, "out.mp4")
        proc = subprocess.run(
            [
                "ffmpeg", "-y", "-loglevel", "error",
                "-framerate", str(fps),
                "-i", os.path.join(work, "%05d.png"),
                "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                out_path,
            ],
            check=False,
            capture_output=True,
        )
        if proc.returncode != 0:
            raise RuntimeError(
                f"ffmpeg sequence→mp4 failed: {proc.stderr.decode()[:500]}"
            )
        with open(out_path, "rb") as f:
            return f.read(), len(paths), width, height
