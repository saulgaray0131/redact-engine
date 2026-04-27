# RedactEngine.Evaluation

Two evaluation harnesses against the deployed inference container.

| Script | Benchmark | Metrics | What it tests |
|---|---|---|---|
| `run_lvis.py` | LVIS v1 val (1,203 categories) | mAP, mAP_r/c/f, mAR | Prompt-driven detection (Grounding DINO) |
| `run_refdavis.py` | Ref-DAVIS17 val | J, F, J&F | Referring-expression localization across video frames |

Both scripts hit the production inference container's synchronous `/detect`
endpoint over HTTPS using `X-Inference-Key` for auth. They never touch
`/redact` (asynchronous, side-effecting on blob storage).

---

## Setup

```bash
cd RedactEngine.Evaluation
python -m venv .venv-eval
source .venv-eval/bin/activate
pip install -r requirements-eval.txt

# ffmpeg must be on PATH — both scripts shell out to it to encode mp4s.
ffmpeg -version

# Point at the prod inference FQDN. Get it with:
#   az containerapp show -n redactengine-prod-inference -g redactengine-prod \
#     --query properties.configuration.ingress.fqdn -o tsv
export INFERENCE_BASE_URL=https://<prod-inference-fqdn>
export INFERENCE_SERVICE_KEY=<value of inference_service_key in prod.tfvars>

# Optional tuning
export INFERENCE_HTTP_TIMEOUT=180   # seconds per request
export INFERENCE_HTTP_RETRIES=3
```

---

## Datasets

### LVIS v1

```
mkdir -p data/lvis
cd data/lvis
# Annotations (~285 MB)
curl -LO https://s3-us-west-2.amazonaws.com/dl.fbaipublicfiles.com/LVIS/lvis_v1_val.json.zip
unzip lvis_v1_val.json.zip
# Images = COCO 2017 val (~1 GB, 5k images) plus the LVIS additions if you
# want the full 19.8k val set. For a fast smoke test, COCO val2017 alone is
# enough to score most categories.
curl -LO http://images.cocodataset.org/zips/val2017.zip
unzip val2017.zip
```

Run:

```bash
python run_lvis.py \
  --annotations data/lvis/lvis_v1_val.json \
  --images-dir  data/lvis/val2017 \
  --output      results/lvis \
  --max-images  500            # smoke test; remove for full val
```

Outputs:

- `results/lvis/predictions.json` — raw `[{image_id, category_id, bbox, score}]`
- `results/lvis/summary.json` — `{AP, AP50, AP75, APr, APc, APf, AR, ARr, ARc, ARf, ...}`

### DAVIS17 + Ref-DAVIS17

DAVIS17 ships with the bare frames + GT masks. Ref-DAVIS17 (Khoreva et al. 2018)
adds two human-annotated text expressions per object.

```
mkdir -p data
cd data
# DAVIS 2017 trainval (480p — what the eval expects)
curl -LO https://data.vision.ee.ethz.ch/csergi/share/davis/DAVIS-2017-trainval-480p.zip
unzip DAVIS-2017-trainval-480p.zip   # → data/DAVIS

# Ref-DAVIS17 expressions
git clone https://github.com/sergiotasconmorales/Ref-DAVIS17 ref-davis17-text
# expressions land in ref-davis17-text/davis_text_annotations/Davis17_annot{1,2}.txt
```

Run (one annotator at a time — there are two; standard practice is to report
the mean across both):

```bash
python run_refdavis.py \
  --davis-root            data/DAVIS \
  --refdavis-expressions  data/ref-davis17-text/davis_text_annotations/Davis17_annot1.txt \
  --output                results/refdavis-annot1
```

Outputs:

- `results/refdavis-annot1/masks/<sequence>/<frame>.png` — palette-mode predicted masks
- `results/refdavis-annot1/inference_summary.json` — inference-side counters
- `results/refdavis-annot1/summary.json` — `{J_mean, F_mean, JandF_mean, ...}`

To re-score without re-querying inference:

```bash
python run_refdavis.py --reuse-masks results/refdavis-annot1/masks --output results/refdavis-annot1 ...
```

---

## Ref-DAVIS17 modes

`run_refdavis.py` has two `--mode` options:

- **`track` (default)** — calls the inference service's `/track` endpoint,
  which returns SAM 2's actual per-pixel masks. This is what produces the
  J / F numbers you'd quote.
- **`bbox`** — calls `/detect` and approximates each mask as the rectangular
  footprint of its DINO bbox, propagated to neighbouring frames by nearest
  anchor. Numbers are a strict **lower bound** on real J / F; useful only
  for sanity-checking or if `/track` is unavailable.

`/track` is gated on `INFERENCE_MODE=real` in the inference service — mock
mode returns 501.

---

## Operational notes

**Cost**: each `/detect` call wakes up the GPU container if it scaled to zero
and runs DINO once per anchor. Full LVIS val with per-image-GT prompts is
roughly 50k requests; DAVIS17 val is 30 sequences × 2–3 expressions = ~80
requests. Budget GPU minutes accordingly, and consider running with
`--max-images` first.

**Cold starts**: the container's first request after scale-to-zero loads DINO
+ SAM 2 weights, which can take 30–60s. The default `INFERENCE_HTTP_TIMEOUT`
of 180s handles that; reduce only after you've warmed the container.

**Resumability**: both scripts write their inference output (predictions JSON,
mask PNGs) before scoring. If the eval step crashes — bad annotation download,
missing dependency, etc. — re-run with `--reuse-predictions` / `--reuse-masks`
to score without re-paying for the inference time.

**Confidence threshold**: `run_lvis.py` defaults to 0.05 (low) so the AP curve
has enough detections to integrate over. `run_refdavis.py` defaults to 0.2
(closer to the prod default of 0.3) since J / F are mass-based metrics that
don't benefit from low-confidence noise.
