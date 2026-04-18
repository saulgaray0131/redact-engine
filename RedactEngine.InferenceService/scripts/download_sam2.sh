#!/usr/bin/env bash
#
# Downloads the SAM 2 checkpoint used by the inference service into ./checkpoints/.
# Safe to re-run; skips the download if the file already exists.
#
# Defaults to sam2.1_hiera_tiny (~155 MB). Override via:
#   SAM2_VARIANT=sam2.1_hiera_small bash scripts/download_sam2.sh
#
# Supported variants: tiny, small, base_plus, large (see
# https://github.com/facebookresearch/sam2#download-checkpoints).

set -euo pipefail

VARIANT="${SAM2_VARIANT:-sam2.1_hiera_tiny}"
CHECKPOINT_DIR="${CHECKPOINT_DIR:-./checkpoints}"
BASE_URL="https://dl.fbaipublicfiles.com/segment_anything_2/092824"

mkdir -p "$CHECKPOINT_DIR"
TARGET="${CHECKPOINT_DIR}/${VARIANT}.pt"

if [[ -f "$TARGET" ]]; then
  echo "Checkpoint already exists at $TARGET — skipping download."
  exit 0
fi

URL="${BASE_URL}/${VARIANT}.pt"
echo "Downloading ${VARIANT} from ${URL}"
echo "Target: ${TARGET}"

# -f: fail on HTTP errors; -L: follow redirects; -o: output path; --progress-bar: cleaner UX
curl -fL --progress-bar -o "$TARGET" "$URL"

echo "Done. $(du -h "$TARGET" | cut -f1) written to $TARGET"
