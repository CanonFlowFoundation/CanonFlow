#!/bin/bash
set -e

IMAGE_NAME="ghcr.io/canonflowfoundation/canonflow-evaluator:0.1.0-alpha"

echo "Building Docker Image: $IMAGE_NAME"
docker build -t "$IMAGE_NAME" .

echo "Image built successfully!"

