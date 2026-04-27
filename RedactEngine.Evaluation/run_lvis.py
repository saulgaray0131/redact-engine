"""
LVIS detection eval for RedactEngine.

Goal — measure how well the deployed prompt-driven detection pipeline (Grounding
DINO behind the inference service /detect endpoint) localizes prompted objects
on LVIS val, the standard 1,203-category long-tail benchmark. We score with
LVIS's official mAP / mAR over the rare/common/frequent splits.

Pipeline per image:

    1. Load the LVIS image.
    2. For each unique category that has at least one ground-truth annotation
       in the val set (or all 1,203 if --all-categories), wrap the image as a
       1-frame mp4 and POST it to /detect with the category's display name as
       the natural-language prompt.
    3. Map each returned bbox into the LVIS prediction format
       {image_id, category_id, bbox: [x,y,w,h], score} and append to the run's
       prediction file.
    4. After all images are processed, evaluate with lvis.LVISEval(iou_type="bbox")
       and dump the summary (mAP, mAP_r, mAP_c, mAP_f, mAR_*) to JSON.

Usage:

    export INFERENCE_BASE_URL=https://<prod-inference-fqdn>
    export INFERENCE_SERVICE_KEY=<key>
    python run_lvis.py \\
        --annotations /data/lvis/lvis_v1_val.json \\
        --images-dir  /data/lvis/val2017 \\
        --output      ./results/lvis \\
        --max-images  500          # optional smoke-test cap

Notes:

  * LVIS val has ~20k images. At ~1s/request, scoring per-image-categories-only
    is ~minutes; --all-categories is hours and only worth it if you want
    open-vocabulary recall numbers (it'll mostly score 0 for absent categories).
  * Confidence threshold is intentionally low (0.05) so the AP curve has enough
    detections to integrate over. Increase via --confidence-threshold if you
    want to mirror the production default of 0.3.
  * The script writes the raw predictions JSON before evaluating, so a crashed
    eval run can be re-scored without re-querying the inference service.
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
import time
from pathlib import Path

import cv2
from tqdm import tqdm

from _inference_client import InferenceClient, encode_single_image_as_mp4


logger = logging.getLogger("run_lvis")


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Score RedactEngine /detect on LVIS val")
    p.add_argument("--annotations", required=True, help="Path to lvis_v1_val.json")
    p.add_argument("--images-dir", required=True, help="Directory of val2017 jpgs")
    p.add_argument("--output", default="./results/lvis", help="Output directory")
    p.add_argument(
        "--confidence-threshold", type=float, default=0.05,
        help="Threshold sent to /detect (default 0.05 — low for AP integration)",
    )
    p.add_argument(
        "--max-images", type=int, default=0,
        help="Cap on images processed; 0 = no cap (full val)",
    )
    p.add_argument(
        "--all-categories", action="store_true",
        help=(
            "Query every one of the 1,203 LVIS categories per image. "
            "Default is to only query categories with GT annotations in this image."
        ),
    )
    p.add_argument(
        "--predictions-only", action="store_true",
        help="Skip evaluation step; only write predictions JSON",
    )
    p.add_argument(
        "--reuse-predictions",
        help="Path to an existing predictions.json — skip inference, just score it",
    )
    return p.parse_args()


def load_lvis_categories(annotations_path: str) -> dict[int, str]:
    """Return {category_id: display_name}. Display name = first synonym."""
    with open(annotations_path) as f:
        data = json.load(f)
    out: dict[int, str] = {}
    for c in data["categories"]:
        # LVIS categories list a `name` (snake_case) and `synonyms`.
        # `synonyms[0]` is the most natural phrasing.
        synonyms = c.get("synonyms") or [c["name"]]
        out[c["id"]] = synonyms[0].replace("_", " ")
    return out


def index_image_categories(annotations_path: str) -> dict[int, set[int]]:
    """Return {image_id: {category_ids that appear in the image}}."""
    with open(annotations_path) as f:
        data = json.load(f)
    out: dict[int, set[int]] = {}
    for ann in data["annotations"]:
        out.setdefault(ann["image_id"], set()).add(ann["category_id"])
    return out


def list_images(annotations_path: str, images_dir: str) -> list[tuple[int, str]]:
    """Return [(image_id, absolute jpg path), ...]."""
    with open(annotations_path) as f:
        data = json.load(f)
    out: list[tuple[int, str]] = []
    for img in data["images"]:
        # LVIS file_name in v1 is "<subdir>/<basename>.jpg"; some downloads
        # flatten to just the basename. Try both.
        candidates = [
            os.path.join(images_dir, img["file_name"]),
            os.path.join(images_dir, os.path.basename(img["file_name"])),
        ]
        path = next((p for p in candidates if os.path.exists(p)), None)
        if path is not None:
            out.append((img["id"], path))
    return out


def run_inference(args: argparse.Namespace) -> Path:
    out_dir = Path(args.output)
    out_dir.mkdir(parents=True, exist_ok=True)
    predictions_path = out_dir / "predictions.json"

    categories = load_lvis_categories(args.annotations)
    image_to_cats = index_image_categories(args.annotations)
    images = list_images(args.annotations, args.images_dir)
    if args.max_images > 0:
        images = images[: args.max_images]

    logger.info(
        "LVIS run: %d images, %s category mode",
        len(images),
        "all 1,203" if args.all_categories else "per-image GT only",
    )

    predictions: list[dict] = []
    started = time.time()
    failures = 0

    with InferenceClient() as client:
        for image_id, image_path in tqdm(images, desc="LVIS images"):
            image = cv2.imread(image_path)
            if image is None:
                logger.warning("Could not read %s — skipping", image_path)
                continue

            try:
                video_bytes = encode_single_image_as_mp4(image)
            except Exception:
                logger.exception("Failed to encode %s — skipping", image_path)
                continue

            if args.all_categories:
                target_cat_ids = list(categories.keys())
            else:
                target_cat_ids = sorted(image_to_cats.get(image_id, set()))

            for cat_id in target_cat_ids:
                prompt = categories[cat_id]
                try:
                    response = client.detect(
                        video_bytes,
                        prompt=prompt,
                        confidence_threshold=args.confidence_threshold,
                    )
                except Exception as exc:
                    failures += 1
                    logger.warning(
                        "detect failed for image=%s cat=%s: %s",
                        image_id, cat_id, exc,
                    )
                    continue

                # The single anchor frame is index 0; pull all detections.
                for frame in response.results:
                    for box in frame.detections:
                        predictions.append(
                            {
                                "image_id": image_id,
                                "category_id": cat_id,
                                "bbox": [
                                    round(box.x, 2),
                                    round(box.y, 2),
                                    round(box.width, 2),
                                    round(box.height, 2),
                                ],
                                "score": round(box.confidence, 4),
                            }
                        )

    elapsed = time.time() - started
    logger.info(
        "Inference done in %.1fs: %d predictions, %d failed requests",
        elapsed, len(predictions), failures,
    )

    with open(predictions_path, "w") as f:
        json.dump(predictions, f)
    logger.info("Wrote predictions → %s", predictions_path)
    return predictions_path


def evaluate(annotations_path: str, predictions_path: Path, output_dir: Path) -> dict:
    """Run lvis.LVISEval(bbox) and return the summary dict."""

    # Imported here so --predictions-only runs don't require `lvis` to be
    # importable in the inference environment.
    from lvis import LVIS, LVISEval, LVISResults

    lvis_gt = LVIS(annotations_path)
    lvis_dt = LVISResults(lvis_gt, str(predictions_path))
    evaluator = LVISEval(lvis_gt, lvis_dt, iou_type="bbox")
    evaluator.run()
    evaluator.print_results()

    summary = {k: float(v) for k, v in evaluator.results.items()}
    summary_path = output_dir / "summary.json"
    with open(summary_path, "w") as f:
        json.dump(summary, f, indent=2)
    logger.info("Wrote summary → %s", summary_path)
    return summary


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
    )
    args = parse_args()

    if args.reuse_predictions:
        predictions_path = Path(args.reuse_predictions)
        out_dir = Path(args.output)
        out_dir.mkdir(parents=True, exist_ok=True)
    else:
        predictions_path = run_inference(args)

    if args.predictions_only:
        return 0

    out_dir = Path(args.output)
    evaluate(args.annotations, predictions_path, out_dir)
    return 0


if __name__ == "__main__":
    sys.exit(main())
