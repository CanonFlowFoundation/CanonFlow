#!/bin/bash
set -e

IMAGE_NAME="ghcr.io/canonflowfoundation/canonflow-evaluator:0.1.0-alpha"
VERSION="0.1.0-alpha"
BUNDLE_DIR="canonflow-evaluator-airgap-${VERSION}"

echo "Creating Air-Gap Bundle: $BUNDLE_DIR"

rm -rf "$BUNDLE_DIR"
mkdir -p "$BUNDLE_DIR/images"
mkdir -p "$BUNDLE_DIR/profiles"
mkdir -p "$BUNDLE_DIR/public-keys"
mkdir -p "$BUNDLE_DIR/schemas"
mkdir -p "$BUNDLE_DIR/examples"
mkdir -p "$BUNDLE_DIR/sbom"

echo "Saving docker image..."
docker save -o "$BUNDLE_DIR/images/canonflow-evaluator.tar" "$IMAGE_NAME"

if [ -d "profiles" ]; then
    cp -r profiles/* "$BUNDLE_DIR/profiles/" || true
fi

echo "Extracting SBOM from Docker image..."
if command -v syft &> /dev/null; then
    syft packages "$IMAGE_NAME" -o spdx-json > "$BUNDLE_DIR/sbom/bom.json"
else
    echo "syft not found, skipping SBOM generation."
fi

echo "Generating checksums..."
cd "$BUNDLE_DIR"
find . -type f -not -name "checksums.sha256" -exec sha256sum {} + | sed "s|./||" > checksums.sha256
cd ..

echo "Air-gap distribution ready in: $BUNDLE_DIR/"

