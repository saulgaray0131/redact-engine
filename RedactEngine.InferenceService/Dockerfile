FROM python:3.12-slim

WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends libgl1 libglib2.0-0 ffmpeg && \
    rm -rf /var/lib/apt/lists/*

COPY RedactEngine.InferenceService/requirements.txt .
RUN pip install --no-cache-dir torch==2.5.1 --index-url https://download.pytorch.org/whl/cpu && \
    pip install --no-cache-dir -r requirements.txt

ENV HF_HOME=/app/.cache/huggingface
RUN python -c "from transformers import AutoProcessor, AutoModelForZeroShotObjectDetection; \
AutoProcessor.from_pretrained('IDEA-Research/grounding-dino-tiny'); \
AutoModelForZeroShotObjectDetection.from_pretrained('IDEA-Research/grounding-dino-tiny')"

COPY RedactEngine.InferenceService/app/ ./app/

ENV INFERENCE_MODE=real

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
