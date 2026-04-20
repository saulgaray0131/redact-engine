FROM python:3.12-slim

WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends libgl1 libglib2.0-0 ffmpeg git curl && \
    rm -rf /var/lib/apt/lists/*

COPY RedactEngine.InferenceService/requirements.txt .
# Install torch built against CUDA 11.8. ACA's serverless GPU nodes report
# driver CUDA 12.4.0 but the torch 2.6 cu124 wheels fail initialization
# there ("NVIDIA driver too old"). cu118 wheels work with any driver
# supporting CUDA 11.8+, which every reasonable modern driver does —
# safest broadly-compatible choice for the T4 profile.
RUN pip install --no-cache-dir torch==2.6.0 --index-url https://download.pytorch.org/whl/cu118 && \
    pip install --no-cache-dir -r requirements.txt

ENV HF_HOME=/app/.cache/huggingface
RUN python -c "from transformers import AutoProcessor, AutoModelForZeroShotObjectDetection; \
AutoProcessor.from_pretrained('IDEA-Research/grounding-dino-tiny'); \
AutoModelForZeroShotObjectDetection.from_pretrained('IDEA-Research/grounding-dino-tiny')"

# Pre-download the SAM 2 checkpoint so the container is self-contained at runtime.
# sam2.1_hiera_tiny is ~155 MB; hosted by Meta AI Research.
ENV SAM2_CHECKPOINT=/app/checkpoints/sam2.1_hiera_tiny.pt
RUN mkdir -p /app/checkpoints && \
    curl -fsSL -o /app/checkpoints/sam2.1_hiera_tiny.pt \
      https://dl.fbaipublicfiles.com/segment_anything_2/092824/sam2.1_hiera_tiny.pt

COPY RedactEngine.InferenceService/app/ ./app/

ENV INFERENCE_MODE=real

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
