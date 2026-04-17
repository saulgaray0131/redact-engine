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
**Status:** Not Started

The proposal describes a two-stage pipeline: Grounding DINO localizes objects in an anchor frame, then SAM 2 propagates pixel-accurate masks across all remaining frames. Currently redaction is applied using bounding boxes only. Integrating SAM 2 would upgrade the pipeline to mask-level redaction that handles occlusions and appearance changes, producing significantly cleaner output especially for irregular shapes.

---

### 5. Evaluation Harness (LVIS + Ref-DAVIS17 Benchmarks)
**Status:** Not Started

The proposal commits to quantitative evaluation on two public benchmarks. LVIS (1,000+ categories, 164K images) measures detection accuracy via mAP and mAR. Ref-DAVIS17 (video segmentation with natural-language expressions) measures tracking quality via region similarity (J) and contour accuracy (F). Build a script or test harness that runs the pipeline against these datasets and computes the relevant metrics so results can be reported.
