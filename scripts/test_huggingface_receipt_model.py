from __future__ import annotations

import argparse
import json
import os
from pathlib import Path

import torch
from PIL import Image
from transformers import AutoImageProcessor, AutoModelForImageClassification


def main() -> int:
    parser = argparse.ArgumentParser(description="Classifica uma imagem no modelo privado de recibos.")
    parser.add_argument("--image", required=True)
    parser.add_argument(
        "--model",
        default="brendonmascarenhas/train-identify-recibos",
    )
    args = parser.parse_args()

    token = os.environ.pop("HF_TOKEN", "").strip()
    if not token:
        raise RuntimeError("HF_TOKEN nao foi fornecido ao processo local.")

    image_path = Path(args.image).resolve(strict=True)
    processor = AutoImageProcessor.from_pretrained(args.model, token=token, use_fast=False)
    model = AutoModelForImageClassification.from_pretrained(args.model, token=token)
    model.eval()

    with Image.open(image_path) as source:
        image = source.convert("RGB")
        inputs = processor(images=image, return_tensors="pt")

    with torch.inference_mode():
        probabilities = torch.nn.functional.softmax(model(**inputs).logits[0], dim=-1)

    labels = model.config.id2label
    results = [
        {"label": labels.get(index, str(index)), "score": float(score)}
        for index, score in enumerate(probabilities.tolist())
    ]
    results.sort(key=lambda item: item["score"], reverse=True)
    print(json.dumps(results, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
