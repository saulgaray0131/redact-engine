# RedactEngine Project Plan

Tracking the remaining work to bring the prompt-driven video redaction engine from working MVP to proposal-complete. Each task is scoped to be tackled independently.

## Tasks

### 1. Frontend UI Controls (Redaction Style + Confidence Threshold)
**Status:** Complete

The backend already accepts `redactionStyle` (Blur, Pixelate, Fill) and `confidenceThreshold` (0.0-1.0, default 0.3) on the job creation endpoint, but the frontend hardcodes defaults and exposes no controls. Add a redaction style dropdown/radio group and a confidence threshold slider to the new job form in `new-job.tsx`. This is purely a frontend change with no backend work required.

---

### 2. Wire Up Grounding DINO in the Inference Service
**Status:** Complete

The inference service currently runs in mock mode, returning random bounding boxes regardless of the prompt. Replace the stubbed `_detect_objects()` function with real Grounding DINO inference so that text prompts actually localize described objects in video frames. This is the core capability described in the proposal. The `INFERENCE_MODE` env var already switches between mock and real mode; the real path just raises `NotImplementedError` today.

---

### 3. Detection Preview Image Generation
**Status:** Complete

When detection completes, the worker should render bounding boxes onto an anchor frame, upload that image to blob storage, and populate the job's `detectionPreviewUrl` field. The frontend's AwaitingReview step should then display this image so users can visually confirm what was detected before approving redaction. Right now users confirm blind with only an object count.

---

### 4. Wire Up SAM 2 for Mask-Level Tracking
**Status:** In Progress

The proposal describes a two-stage pipeline: Grounding DINO localizes objects in an anchor frame, then SAM 2 propagates pixel-accurate masks across all remaining frames. Currently redaction is applied using bounding boxes only. Integrating SAM 2 would upgrade the pipeline to mask-level redaction that handles occlusions and appearance changes, producing significantly cleaner output especially for irregular shapes.

---

### 5. Evaluation Harness (LVIS + Ref-DAVIS17 Benchmarks)
**Status:** Not Started

The proposal commits to quantitative evaluation on two public benchmarks. LVIS (1,000+ categories, 164K images) measures detection accuracy via mAP and mAR. Ref-DAVIS17 (video segmentation with natural-language expressions) measures tracking quality via region similarity (J) and contour accuracy (F). Build a script or test harness that runs the pipeline against these datasets and computes the relevant metrics so results can be reported.

---

### 6. Natural Language Prompt Translation (Azure OpenAI)
**Status:** Complete

Grounding DINO expects prompts in a specific format — short, period-separated noun phrases like `person. license plate. laptop screen.` — but users naturally write instructions like "blur out anyone walking past and any visible screens." Today the user's raw prompt is passed straight to DINO, and detection quality suffers when phrasing doesn't match what the model was trained on. Add an Azure OpenAI-backed translation step that converts free-form user intent into a DINO-compatible prompt.

**Scope:**
- **Backend (API Service):** On job creation, call Azure OpenAI (gpt-4o-mini or similar) with a system prompt that teaches the structure DINO expects and returns structured JSON (e.g. `{ "targets": ["person", "license plate", ...] }`). Store both the original `userPrompt` and the derived `detectionPrompt` on the Job entity (new migration). The worker uses `detectionPrompt` when calling inference.
- **Frontend (AwaitingReview):** Display the user's original prompt alongside the translated DINO prompt in the review step (next to the detection preview from task 3), and allow the user to edit the translated prompt and re-run detection if the translation missed intent.
- **Config:** Add Azure OpenAI endpoint / deployment / key settings wired through Aspire + `appsettings.json`, with a mock/passthrough mode (analogous to `INFERENCE_MODE`) so local dev doesn't require an Azure key.
- **Failure mode:** If translation fails or Azure OpenAI is unreachable, fall back to passing the raw user prompt through unchanged and surface a warning on the job — don't block job creation.
