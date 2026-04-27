# Proposal: add `/track` endpoint to RedactEngine.InferenceService

**Status:** draft, not applied. Awaiting Reya's approval per the project rule.

## Why

The Ref-DAVIS17 eval needs SAM 2's per-pixel masks. Today the only endpoints
that touch SAM 2 are `/detect` (returns boxes only) and `/redact` (async,
composites the redaction onto the video and uploads to blob storage). Neither
exposes raw masks.

Adding `/track` lets `run_refdavis.py` report real J & F instead of the
bbox-rectangle lower bound it currently approximates.

## Shape of the change

Two pieces of work, both in `RedactEngine.InferenceService/app/main.py`:

1. **Refactor**: lift the SAM 2 mask-computation half of `_redact_video_sam2`
   into a new helper `_track_video_sam2`. `_redact_video_sam2` then calls it
   and continues with the compositing + ffmpeg encode step. Behaviour of
   `/redact` is unchanged — the existing flow still produces the same bytes.
2. **New endpoint**: `POST /track` that takes the same inputs as `/detect`
   (multipart video + prompt + confidence_threshold), calls the helper, and
   returns per-frame masks as base64-encoded PNGs.

I picked refactor + new endpoint over a copy-paste duplicate because the
SAM 2 code is ~100 lines and duplicating it would mean every future fix
(dtype bugs, dim-mismatch fixes, etc.) has to be applied twice. The refactor
is a pure lift — no logic changes — so the `/redact` path stays bit-for-bit
identical.

If you'd rather minimize risk to the working `/redact` path, I can do the
duplicate-code version instead. Tell me which.

---

## Diff against `RedactEngine.InferenceService/app/main.py`

### 1. New response models (add near the existing `DetectResponse`)

```python
class TrackedFrame(BaseModel):
    """One frame's union-of-objects mask for a /track response."""
    frame_index: int
    # PNG-encoded 1-bit mask, base64'd. Pixels=255 where the prompt's
    # object is present, 0 elsewhere. Frames with no mask are omitted
    # from the response — clients should default-fill those frames as
    # all-zero of (height, width).
    png_base64: str


class TrackResponse(BaseModel):
    job_id: str
    prompt: str
    frame_count: int
    width: int
    height: int
    masks: list[TrackedFrame]
```

### 2. Extract helper `_track_video_sam2` from existing `_redact_video_sam2`

The new helper is exactly the body of `_redact_video_sam2` from line 866 to
line 1016 (everything up to and including `masks_by_frame` computation),
returning the dimensions and the masks dict instead of falling through into
the ffmpeg encode step.

```python
def _track_video_sam2(
    app: FastAPI,
    video_bytes: bytes,
    prompt: str,
    confidence_threshold: float,
) -> tuple[int, int, int, float, dict[int, np.ndarray]]:
    """
    Run the DINO + SAM 2 mask-generation pipeline. Stops after computing
    per-frame masks; does NOT composite a redaction or re-encode the
    video.

    Returns (width, height, frame_count, fps, masks_by_frame). Callers
    that want to redact then composite under each mask and re-encode
    (this is what _redact_video_sam2 does). Callers that only want the
    masks for evaluation (the new /track endpoint) consume them directly.

    The masks dict only contains entries for frames where SAM 2 produced
    a non-empty mask. Frames absent from the dict should be treated as
    all-zero of shape (height, width).

    Owns + cleans up its own temp working directory.
    """
    torch = app.state.torch
    predictor = app.state.sam2_predictor
    sam2_device = app.state.sam2_device

    work_dir = tempfile.mkdtemp(prefix="track_sam2_")
    tmp_in = os.path.join(work_dir, "input.mp4")
    frames_dir = os.path.join(work_dir, "frames")

    try:
        with open(tmp_in, "wb") as f:
            f.write(video_bytes)

        fps, width, height, frame_count = _extract_all_frames(tmp_in, frames_dir)
        if frame_count == 0:
            raise HTTPException(status_code=400, detail="Could not extract frames from video")

        normalized_prompt = _normalize_prompt(prompt)

        # ── DINO seed detections on anchor frames ──────────────────────
        anchors = _read_anchor_frames_from_dir(frames_dir, frame_count, fps)
        anchor_detections = _run_grounding_dino_batch(
            app,
            [frame for _, frame in anchors],
            normalized_prompt,
            confidence_threshold,
        )
        seeds: list[tuple[int, BoundingBox]] = [
            (frame_idx, det)
            for (frame_idx, _frame), detections in zip(anchors, anchor_detections)
            for det in detections
        ]

        logger.info(
            "Track: %d frames, %d anchors, %d seed detections",
            frame_count, len(anchors), len(seeds),
        )

        if not seeds:
            return width, height, frame_count, fps, {}

        # ── SAM 2 frame subsampling (identical to _redact_video_sam2) ──
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
                try:
                    os.link(src, dst)
                except OSError:
                    shutil.copy(src, dst)
            frames_to_use_dir = sampled_dir
            logger.info(
                "Track subsample: %d/%d frames (cap=%d)",
                len(sampled_orig), frame_count, SAM2_MAX_FRAMES,
            )
        else:
            sampled_orig = list(range(frame_count))
            frames_to_use_dir = frames_dir

        def nearest_sampled_idx(orig_idx: int) -> int:
            i = bisect.bisect_left(sampled_orig, orig_idx)
            if i == 0:
                return 0
            if i >= len(sampled_orig):
                return len(sampled_orig) - 1
            before = sampled_orig[i - 1]
            after = sampled_orig[i]
            return i if (after - orig_idx) < (orig_idx - before) else i - 1

        autocast_ctx = (
            torch.autocast(device_type=sam2_device, dtype=torch.bfloat16)
            if sam2_device in ("cuda", "cpu")
            else _nullcontext()
        )
        masks_by_sampled: dict[int, np.ndarray] = {}
        with torch.inference_mode(), autocast_ctx:
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

            for s_idx, _obj_ids, mask_logits in predictor.propagate_in_video(
                inference_state
            ):
                bool_masks = (mask_logits > 0.0).squeeze(1).cpu().numpy()
                union = np.any(bool_masks, axis=0) if bool_masks.size else None
                if union is not None and union.any():
                    masks_by_sampled[s_idx] = union

        masks_by_frame: dict[int, np.ndarray] = {}
        for orig_idx in range(frame_count):
            s_idx = nearest_sampled_idx(orig_idx)
            mask = masks_by_sampled.get(s_idx)
            if mask is not None:
                masks_by_frame[orig_idx] = mask

        logger.info(
            "Track: masks for %d/%d sampled frames, expanded to %d original frames",
            len(masks_by_sampled), len(sampled_orig), len(masks_by_frame),
        )
        return width, height, frame_count, fps, masks_by_frame
    finally:
        shutil.rmtree(work_dir, ignore_errors=True)
```

### 3. Slim down `_redact_video_sam2` to call the helper

The first ~150 lines of `_redact_video_sam2` are replaced by a single call.
The remaining body (ffmpeg encode + composite loop) is unchanged.

```python
def _redact_video_sam2(
    app: FastAPI,
    video_bytes: bytes,
    prompt: str,
    style: RedactionStyle,
    confidence_threshold: float,
) -> bytes:
    """
    Real-mode redaction pipeline. Delegates mask generation to
    _track_video_sam2, then composites the redaction style under each
    mask and re-encodes via ffmpeg.
    """
    width, height, frame_count, fps, masks_by_frame = _track_video_sam2(
        app, video_bytes, prompt, confidence_threshold
    )

    work_dir = tempfile.mkdtemp(prefix="redact_sam2_")
    tmp_out = os.path.join(work_dir, "output.mp4")
    process = None

    try:
        # If DINO found nothing to track, return the original re-encoded.
        # (Need the frames on disk again to do this — re-extract.)
        if not masks_by_frame:
            frames_dir = os.path.join(work_dir, "frames")
            tmp_in = os.path.join(work_dir, "input.mp4")
            with open(tmp_in, "wb") as f:
                f.write(video_bytes)
            _extract_all_frames(tmp_in, frames_dir)
            return _reencode_from_frames(frames_dir, tmp_out, fps)

        # Re-extract frames for compositing — _track_video_sam2 cleans up
        # its own working directory, so the JPEGs it produced are gone.
        frames_dir = os.path.join(work_dir, "frames")
        tmp_in = os.path.join(work_dir, "input.mp4")
        with open(tmp_in, "wb") as f:
            f.write(video_bytes)
        _extract_all_frames(tmp_in, frames_dir)

        # ── Composite + re-encode (identical to existing code) ─────────
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

        frame_queue: queue.Queue = queue.Queue(maxsize=4)
        sentinel = object()
        producer_err: list[BaseException] = []

        def _produce_frames() -> None:
            try:
                for idx in range(frame_count):
                    frame_queue.put(
                        (idx, cv2.imread(os.path.join(frames_dir, f"{idx:05d}.jpg")))
                    )
            except BaseException as exc:
                producer_err.append(exc)
            finally:
                frame_queue.put(sentinel)

        producer = threading.Thread(target=_produce_frames, daemon=True)
        producer.start()
        try:
            while True:
                item = frame_queue.get()
                if item is sentinel:
                    break
                frame_idx, frame = item
                if frame is None:
                    continue
                mask = masks_by_frame.get(frame_idx)
                if mask is not None:
                    frame = _apply_redaction_mask(frame, mask, style)
                process.stdin.write(frame.tobytes())
        finally:
            producer.join(timeout=5)
        if producer_err:
            raise producer_err[0]

        _, stderr = process.communicate(timeout=600)
        if process.returncode != 0:
            logger.error("ffmpeg re-encode failed: %s", stderr.decode())
            raise RuntimeError("ffmpeg re-encode failed")

        with open(tmp_out, "rb") as f:
            return f.read()
    finally:
        if process is not None and process.poll() is None:
            process.kill()
        shutil.rmtree(work_dir, ignore_errors=True)
```

⚠️ **Trade-off in this refactor**: `_redact_video_sam2` now extracts frames
to disk **twice** — once inside `_track_video_sam2` (then thrown away),
once again in the redact path for the composite loop. That's an extra
ffmpeg decode pass per /redact call. On the GPU container that's typically
~1–3 seconds for a short clip; meaningful but not catastrophic. If we want
to avoid the redundant decode, the helper can take an optional
`keep_frames_in: str | None` arg and skip its `shutil.rmtree`. Tell me if
you want that wrinkle now or are happy to defer.

### 4. Add the `/track` endpoint

Place this right after `/detect` (around line 308):

```python
@app.post("/track", response_model=TrackResponse)
async def track(
    request: Request,
    video: UploadFile = File(...),
    prompt: str = Form(...),
    confidence_threshold: float = Form(0.3),
):
    """
    Synchronously run DINO + SAM 2 and return per-frame masks.

    Mirrors /detect's I/O shape (multipart video + prompt) but returns
    the same per-pixel masks that /redact would composite a blur under.
    Intended for offline evaluation pipelines that need J/F-style
    pixel metrics. Production redaction flow should keep using /redact.
    """
    if INFERENCE_MODE == "mock":
        raise HTTPException(
            status_code=501,
            detail="/track requires INFERENCE_MODE=real (SAM 2 not loaded in mock mode)",
        )
    if request.app.state.sam2_predictor is None:
        raise HTTPException(status_code=503, detail="SAM 2 still loading")
    if not video.content_type or not video.content_type.startswith("video/"):
        raise HTTPException(status_code=400, detail="File must be a video")

    video_bytes = await video.read()

    # Run the same blocking SAM 2 pipeline /redact uses, off the event
    # loop so we don't block other requests on a long propagation.
    width, height, frame_count, _fps, masks_by_frame = await asyncio.to_thread(
        _track_video_sam2,
        request.app,
        video_bytes,
        prompt,
        confidence_threshold,
    )

    masks_payload: list[TrackedFrame] = []
    for frame_idx, mask in sorted(masks_by_frame.items()):
        # Encode as 1-bit PNG. cv2 wants uint8 0/255.
        mask_u8 = (mask.astype(np.uint8)) * 255
        ok, buf = cv2.imencode(".png", mask_u8)
        if not ok:
            logger.warning("Failed to PNG-encode mask for frame %d", frame_idx)
            continue
        masks_payload.append(
            TrackedFrame(
                frame_index=frame_idx,
                png_base64=base64.b64encode(buf.tobytes()).decode("ascii"),
            )
        )

    return TrackResponse(
        job_id=str(uuid.uuid4()),
        prompt=prompt,
        frame_count=frame_count,
        width=width,
        height=height,
        masks=masks_payload,
    )
```

---

## Eval-side change (companion to the server change)

`run_refdavis.py` would gain a `_track_via_inference` path that calls `/track`
and decodes PNG masks directly. Sketch:

```python
def _decode_track_response(payload: dict, frame_count: int) -> list[np.ndarray]:
    width = int(payload["width"])
    height = int(payload["height"])
    masks_by_idx = {int(m["frame_index"]): m["png_base64"] for m in payload["masks"]}
    out: list[np.ndarray] = []
    for f in range(frame_count):
        b64 = masks_by_idx.get(f)
        if b64 is None:
            out.append(np.zeros((height, width), dtype=np.uint8))
            continue
        png_bytes = base64.b64decode(b64)
        decoded = cv2.imdecode(
            np.frombuffer(png_bytes, dtype=np.uint8), cv2.IMREAD_GRAYSCALE,
        )
        out.append((decoded > 127).astype(np.uint8))
    return out
```

Then the per-object loop in `run_inference()` uses `/track` instead of
`_boxes_to_mask` + `_propagate_anchor_masks`. Everything below that —
PNG palette write, davis2017-evaluation invocation, summary JSON — stays
untouched.

---

## What I'd like you to weigh in on

1. **Refactor (1 helper, no duplication) vs. duplicate code**? I prefer the
   refactor. It does change `_redact_video_sam2` though — fair to push back if
   you'd rather not touch the working /redact path.
2. **Frames-on-disk redundancy** in the refactored `_redact_video_sam2` — fix
   now via the optional `keep_frames_in` arg, or defer?
3. **Mask payload format**: 1-bit PNG base64 (proposed) vs COCO RLE (more
   compact, requires `pycocotools` import on the server). Default DAVIS clips
   are <100 frames at 854×480 → ~50 KB compressed PNG per frame, so total
   payload is single-digit MB per call. PNG seems fine.
4. **Auth / rate-limit**: `/track` is GPU-heavy. Should it require a separate
   `X-Eval-Key` instead of `X-Inference-Key`, so you can revoke eval access
   without breaking prod /redact? My take: no, same key is fine — this is a
   research preview, and the worker is the only legit caller of /redact
   anyway. But happy to add it if you want.

Once you've signed off (with whatever modifications), I'll apply the diff to
`main.py` and update `run_refdavis.py` in the same change.
