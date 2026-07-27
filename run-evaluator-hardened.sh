#!/bin/bash
set -e

IMAGE_NAME="ghcr.io/canonflowfoundation/canonflow-evaluator:0.1.0-alpha"

mkdir -p report

echo "Starting hardened CanonFlow evaluator container..."
docker run --rm \
  --network none \
  --read-only \
  --cap-drop ALL \
  --security-opt no-new-privileges \
  --pids-limit 256 \
  --memory 2g \
  --cpus 2 \
  --tmpfs /tmp:rw,noexec,nosuid,size=128m \
  --mount type=bind,src="$PWD",dst=/input,readonly \
  --mount type=bind,src="$PWD/report",dst=/output \
  "$IMAGE_NAME" \
  evaluate \
  --manifest /input/canonflow-evaluation.json \
  --output /output

