"""
Ref-DAVIS17 referring-expression VOS eval for RedactEngine.

Goal — measure how well the prompt-driven engine localizes and tracks objects
described in natural language across DAVIS17 val sequences. We report the
standard DAVIS metrics: J (region IoU) and F (contour F-measure), averaged
across objects and frames, plus the J&F mean.

Default mode (`--mode track`) calls the inference service's `/track` endpoint,
which returns SAM 2's actual per-pixel masks. This is the eval mode that
produces the J / F numbers we'd quote in a paper.

Fallback mode (`--mode bbox`) calls `/detect` instead and approximates each
mask as the rectangular footprint of its bbox, propagated to neighbouring
frames by nearest-anchor assignment. The numbers it produces are a strict
lower bound on real SAM 2 J / F — keep this around for sanity-checking
before /track is rebuilt or in case the GPU container is unavailable.

Pipeline per sequence (track mode):

    1. Load the JPEG frames for the sequence.
    2. For each (sequence, object_id) referring expression:
       a. Encode the full sequence as an mp4.
       b. POST to /track with the referring expression as the prompt.
       c. /track returns SAM 2 union-mask PNGs per frame; we decode them
          and treat absent frames as all-zero.
    3. Composite each object's per-frame mask into a multi-class palette PNG
       under <output>/masks/<sequence>/<frame>.png.
    4. Run davis2017-evaluation on the predicted mask directory to print
       J / F / J&F per sequence and globally.

Usage:

    export INFERENCE_BASE_URL=https://<prod-inference-fqdn>
    export INFERENCE_SERVICE_KEY=<key>
    python run_refdavis.py \\
        --davis-root /data/DAVIS \\
        --refdavis-expressions /data/Ref-DAVIS17/davis_text_annotations/Davis17_annot1.txt \\
        --output ./results/refdavis \\
        --mode track \\
        --sequences blackswan dance-twirl     # optional subset
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
import time
from collections import defaultdict
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from tqdm import tqdm

from _inference_client import (
    BoundingBox,
    InferenceClient,
    encode_image_sequence_as_mp4,
)


logger = logging.getLogger("run_refdavis")


# DAVIS uses an 8-bit palette where index 0 = background, 1..N = objects.
# davis2017-evaluation reads predictions through PIL; saving as palette-mode
# PNG is the canonical wire format.
DAVIS_PALETTE = [
    0, 0, 0,
    128, 0, 0,
    0, 128, 0,
    128, 128, 0,
    0, 0, 128,
    128, 0, 128,
    0, 128, 128,
    128, 128, 128,
    64, 0, 0,
    191, 0, 0,
    64, 128, 0,
    191, 128, 0,
] + [0] * (768 - 36)


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Score RedactEngine on Ref-DAVIS17 val")
    p.add_argument(
        "--davis-root", required=True,
        help="DAVIS root containing JPEGImages/, Annotations/, ImageSets/",
    )
    p.add_argument(
        "--refdavis-expressions", required=True,
        help=(
            "Path to a Ref-DAVIS17 expression file. The widely-used release "
            "(Khoreva et al. 2018) ships two annotators (Davis17_annot1.txt, "
            "Davis17_annot2.txt). Pick one — we run both in separate runs."
        ),
    )
    p.add_argument(
        "--output", default="./results/refdavis",
        help="Output directory; predicted masks land in <output>/masks/<sequence>/",
    )
    p.add_argument(
        "--mode", choices=("track", "bbox"), default="track",
        help=(
            "track (default): call /track and use SAM 2 masks directly. "
            "bbox: call /detect and approximate masks as bbox rectangles "
            "(strict lower bound on real J / F)."
        ),
    )
    p.add_argument(
        "--sequences", nargs="*", default=None,
        help="Optional list of sequence names to subset (default: full Ref-DAVIS17 val)",
    )
    p.add_argument(
        "--confidence-threshold", type=float, default=0.2,
        help="Threshold sent to /track or /detect (default 0.2)",
    )
    p.add_argument(
        "--fps", type=float, default=24.0,
        help="FPS used when packing the JPEG sequence into mp4 (default 24)",
    )
    p.add_argument(
        "--predictions-only", action="store_true",
        help="Skip davis2017-evaluation; only write predicted masks",
    )
    p.add_argument(
        "--reuse-masks",
        help="Path to an existing masks/ dir — skip inference, just score it",
    )
    return p.parse_args()


# ── Dataset loading ──────────────────────────────────────────────────────────


def load_val_sequences(davis_root: str) -> list[str]:
    """Read DAVIS17 val split: ImageSets/2017/val.txt."""
    split_path = os.path.join(davis_root, "ImageSets", "2017", "val.txt")
    with open(split_path) as f:
        return [line.strip() for line in f if line.strip()]


def load_refdavis_expressions(path: str) -> dict[str, dict[int, str]]:
    """
    Parse a Ref-DAVIS17 expression file.

    The standard format is one line per expression:
        <sequence> <object_id> <expression>
    where object_id matches the palette index in the GT annotation PNGs.

    Returns {sequence: {object_id: expression}}.
    """
    out: dict[str, dict[int, str]] = defaultdict(dict)
    with open(path) as f:
        for raw in f:
            line = raw.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            parts = line.split(maxsplit=2)
            if len(parts) < 3:
                logger.warning("Skipping malformed expression line: %r", line)
                continue
            seq, obj_id_str, expression = parts
            try:
                obj_id = int(obj_id_str)
            except ValueError:
                logger.warning(
                    "Skipping line with non-int object id: %r", line,
                )
                continue
            out[seq][obj_id] = expression.strip().strip('"')
    return out


def list_sequence_frames(davis_root: str, sequence: str) -> list[str]:
    seq_dir = os.path.join(davis_root, "JPEGImages", "480p", sequence)
    return sorted(
        os.path.join(seq_dir, f)
        for f in os.listdir(seq_dir)
        if f.endswith(".jpg")
    )


def get_sequence_dims(frame_paths: list[str]) -> tuple[int, int]:
    img = cv2.imread(frame_paths[0])
    if img is None:
        raise RuntimeError(f"Could not read {frame_paths[0]}")
    h, w = img.shape[:2]
    return h, w


# ── Box → mask plumbing ──────────────────────────────────────────────────────


def _boxes_to_mask(
    boxes: list[BoundingBox],
    height: int,
    width: int,
) -> np.ndarray:
    """Union of axis-aligned bbox footprints — the bbox-mask approximation."""
    mask = np.zeros((height, width), dtype=np.uint8)
    for box in boxes:
        x1 = max(0, int(round(box.x)))
        y1 = max(0, int(round(box.y)))
        x2 = min(width, int(round(box.x + box.width)))
        y2 = min(height, int(round(box.y + box.height)))
        if x2 <= x1 or y2 <= y1:
            continue
        mask[y1:y2, x1:x2] = 1
    return mask


def _propagate_anchor_masks(
    anchor_masks: dict[int, np.ndarray],
    frame_count: int,
    height: int,
    width: int,
) -> list[np.ndarray]:
    """
    For each frame in [0, frame_count), pick the nearest anchor's mask.

    /detect only runs DINO on a sparse set of anchor frames, so we extend each
    anchor's prediction forward/backward to its neighbouring frames using
    nearest-anchor assignment. Frames before the first anchor reuse the first
    anchor's mask; frames after the last anchor reuse the last.
    """
    if not anchor_masks:
        return [np.zeros((height, width), dtype=np.uint8) for _ in range(frame_count)]

    anchor_frames = sorted(anchor_masks.keys())
    out: list[np.ndarray] = []
    for f in range(frame_count):
        # nearest anchor by absolute frame distance
        nearest = min(anchor_frames, key=lambda a: abs(a - f))
        out.append(anchor_masks[nearest])
    return out


def _save_masks(
    sequence_dir: Path,
    object_masks: dict[int, list[np.ndarray]],
    frame_count: int,
    height: int,
    width: int,
) -> None:
    """
    Write one PNG per frame: paletted mask with object IDs as indices.

    object_masks[obj_id][frame_idx] is a binary {0,1} ndarray. We composite
    them into a single multi-class mask per frame; later objects overwrite
    earlier ones at conflicts (same convention as DAVIS GT).
    """
    sequence_dir.mkdir(parents=True, exist_ok=True)
    for f in range(frame_count):
        composite = np.zeros((height, width), dtype=np.uint8)
        for obj_id, masks in sorted(object_masks.items()):
            composite[masks[f] > 0] = obj_id
        img = Image.fromarray(composite, mode="P")
        img.putpalette(DAVIS_PALETTE)
        img.save(sequence_dir / f"{f:05d}.png")


# ── Inference loop ───────────────────────────────────────────────────────────


def run_inference(args: argparse.Namespace) -> Path:
    out_dir = Path(args.output)
    masks_dir = out_dir / "masks"
    masks_dir.mkdir(parents=True, exist_ok=True)

    val_sequences = load_val_sequences(args.davis_root)
    expressions = load_refdavis_expressions(args.refdavis_expressions)

    if args.sequences:
        target = [s for s in val_sequences if s in args.sequences]
    else:
        target = val_sequences

    summary = {
        "sequences_processed": 0,
        "objects_processed": 0,
        "failed_requests": 0,
        "expression_file": args.refdavis_expressions,
    }
    started = time.time()

    with InferenceClient() as client:
        for sequence in tqdm(target, desc="DAVIS sequences"):
            seq_expressions = expressions.get(sequence, {})
            if not seq_expressions:
                logger.warning("No expressions for sequence %s — skipping", sequence)
                continue

            frame_paths = list_sequence_frames(args.davis_root, sequence)
            if not frame_paths:
                logger.warning("No frames for sequence %s — skipping", sequence)
                continue
            height, width = get_sequence_dims(frame_paths)
            frame_count = len(frame_paths)

            try:
                video_bytes, _, _, _ = encode_image_sequence_as_mp4(
                    frame_paths, fps=args.fps,
                )
            except Exception:
                logger.exception("Failed to encode %s — skipping", sequence)
                continue

            object_masks: dict[int, list[np.ndarray]] = {}
            for obj_id, expression in sorted(seq_expressions.items()):
                if args.mode == "track":
                    try:
                        track_resp = client.track(
                            video_bytes,
                            prompt=expression,
                            confidence_threshold=args.confidence_threshold,
                            filename=f"{sequence}.mp4",
                        )
                    except Exception as exc:
                        summary["failed_requests"] += 1
                        logger.warning(
                            "track failed for sequence=%s obj=%d (%r): %s",
                            sequence, obj_id, expression, exc,
                        )
                        object_masks[obj_id] = [
                            np.zeros((height, width), dtype=np.uint8)
                            for _ in range(frame_count)
                        ]
                        continue

                    # /track only includes frames where SAM 2 produced a mask.
                    # Fill the rest with all-zero of the GT dims.
                    masks_by_idx = {
                        m.frame_index: m.mask for m in track_resp.masks
                    }
                    per_frame: list[np.ndarray] = []
                    for f in range(frame_count):
                        m = masks_by_idx.get(f)
                        if m is None:
                            per_frame.append(np.zeros((height, width), dtype=np.uint8))
                        elif m.shape != (height, width):
                            # Server-reported dims can drift from the GT dims if
                            # ffmpeg padded the mp4 to even dimensions. Resize
                            # back to GT dims with nearest-neighbour to preserve
                            # the binary mask.
                            per_frame.append(
                                cv2.resize(
                                    m, (width, height),
                                    interpolation=cv2.INTER_NEAREST,
                                )
                            )
                        else:
                            per_frame.append(m)
                    object_masks[obj_id] = per_frame
                else:
                    # bbox-mask fallback path
                    try:
                        response = client.detect(
                            video_bytes,
                            prompt=expression,
                            confidence_threshold=args.confidence_threshold,
                            filename=f"{sequence}.mp4",
                        )
                    except Exception as exc:
                        summary["failed_requests"] += 1
                        logger.warning(
                            "detect failed for sequence=%s obj=%d (%r): %s",
                            sequence, obj_id, expression, exc,
                        )
                        object_masks[obj_id] = [
                            np.zeros((height, width), dtype=np.uint8)
                            for _ in range(frame_count)
                        ]
                        continue

                    anchor_masks: dict[int, np.ndarray] = {}
                    for frame in response.results:
                        anchor_masks[frame.frame_index] = _boxes_to_mask(
                            frame.detections, height, width,
                        )
                    object_masks[obj_id] = _propagate_anchor_masks(
                        anchor_masks, frame_count, height, width,
                    )
                summary["objects_processed"] += 1

            _save_masks(
                masks_dir / sequence,
                object_masks,
                frame_count,
                height,
                width,
            )
            summary["sequences_processed"] += 1

    summary["elapsed_s"] = round(time.time() - started, 2)
    with open(out_dir / "inference_summary.json", "w") as f:
        json.dump(summary, f, indent=2)
    logger.info("Inference summary: %s", summary)
    return masks_dir


# ── Evaluation ───────────────────────────────────────────────────────────────


def evaluate(
    davis_root: str,
    masks_dir: Path,
    output_dir: Path,
) -> dict:
    """
    Run davis2017-evaluation on the predicted masks.

    The evaluator expects predicted PNGs in <results>/<sequence>/<frame>.png
    matching the GT layout under <davis_root>/Annotations/480p/.
    """

    # Imported here so --predictions-only runs don't require the eval package
    # to be importable in the inference environment.
    from davis2017.evaluation import DAVISEvaluation

    evaluator = DAVISEvaluation(
        davis_root=davis_root,
        task="semi-supervised",
        gt_set="val",
    )
    metrics_res = evaluator.evaluate(str(masks_dir))
    j_metrics = metrics_res["J"]
    f_metrics = metrics_res["F"]

    summary = {
        "J_mean": float(np.mean(j_metrics["M"])),
        "J_recall": float(np.mean(j_metrics["R"])),
        "J_decay": float(np.mean(j_metrics["D"])),
        "F_mean": float(np.mean(f_metrics["M"])),
        "F_recall": float(np.mean(f_metrics["R"])),
        "F_decay": float(np.mean(f_metrics["D"])),
    }
    summary["JandF_mean"] = (summary["J_mean"] + summary["F_mean"]) / 2

    summary_path = output_dir / "summary.json"
    with open(summary_path, "w") as f:
        json.dump(summary, f, indent=2)
    logger.info("Wrote summary → %s", summary_path)
    print(json.dumps(summary, indent=2))
    return summary


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
    )
    args = parse_args()

    if args.reuse_masks:
        masks_dir = Path(args.reuse_masks)
        out_dir = Path(args.output)
        out_dir.mkdir(parents=True, exist_ok=True)
    else:
        masks_dir = run_inference(args)

    if args.predictions_only:
        return 0

    out_dir = Path(args.output)
    evaluate(args.davis_root, masks_dir, out_dir)
    return 0


if __name__ == "__main__":
    sys.exit(main())
