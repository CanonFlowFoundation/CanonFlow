#!/usr/bin/env bash
set -euo pipefail

bundle_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$bundle_dir"

sha256sum -c checksums.sha256
tools/cosign verify-blob \
    --offline \
    --insecure-ignore-tlog \
    --key public-keys/cosign.pub \
    --bundle signatures/image.sigstore.json \
    images/canonflow-evaluator.tar
tools/cosign verify-blob \
    --offline \
    --insecure-ignore-tlog \
    --key public-keys/cosign.pub \
    --bundle signatures/provenance.sigstore.json \
    provenance.json

image_name="$(head -n 1 image-reference.txt)"
docker load --input images/canonflow-evaluator.tar
docker run --rm \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    "$image_name" version
