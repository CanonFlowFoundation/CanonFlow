#!/usr/bin/env bash
set -euo pipefail

image_name="${IMAGE_NAME:-canonflow-evaluator:0.1.0-alpha}"
version="${VERSION:-0.1.0-alpha}"
bundle_dir="${BUNDLE_DIR:-canonflow-evaluator-airgap-${version}}"
cosign_key="${COSIGN_KEY:-}"

bundle_base="$(basename -- "$bundle_dir")"
case "$bundle_base" in
    canonflow-evaluator-airgap-*) ;;
    *)
        echo "BUNDLE_DIR must start with 'canonflow-evaluator-airgap-'." >&2
        exit 64
        ;;
esac

command -v docker >/dev/null 2>&1 || { echo "docker is required." >&2; exit 1; }
command -v syft >/dev/null 2>&1 || { echo "syft is required; refusing to omit the SBOM." >&2; exit 1; }
command -v cosign >/dev/null 2>&1 || { echo "cosign is required; refusing to omit signatures." >&2; exit 1; }
test -n "$cosign_key" && test -f "$cosign_key" || {
    echo "COSIGN_KEY must name the externally managed release-signing key." >&2
    exit 1
}
docker image inspect "$image_name" >/dev/null

if test -e "$bundle_dir"; then
    rm -rf -- "$bundle_dir"
fi
mkdir -p \
    "$bundle_dir/images" \
    "$bundle_dir/profiles" \
    "$bundle_dir/public-keys" \
    "$bundle_dir/schemas" \
    "$bundle_dir/examples" \
    "$bundle_dir/sbom" \
    "$bundle_dir/signatures" \
    "$bundle_dir/tools"

docker save -o "$bundle_dir/images/canonflow-evaluator.tar" "$image_name"
syft "$image_name" -o "spdx-json=$bundle_dir/sbom/sbom.spdx.json"
docker image inspect "$image_name" > "$bundle_dir/provenance.json"

cosign public-key --key "$cosign_key" > "$bundle_dir/public-keys/cosign.pub"
cosign sign-blob \
    --yes \
    --tlog-upload=false \
    --key "$cosign_key" \
    --bundle "$bundle_dir/signatures/image.sigstore.json" \
    "$bundle_dir/images/canonflow-evaluator.tar"
cosign sign-blob \
    --yes \
    --tlog-upload=false \
    --key "$cosign_key" \
    --bundle "$bundle_dir/signatures/provenance.sigstore.json" \
    "$bundle_dir/provenance.json"
cp "$(command -v cosign)" "$bundle_dir/tools/cosign"
chmod 0755 "$bundle_dir/tools/cosign"

if test -d profiles; then
    cp -R profiles/. "$bundle_dir/profiles/"
fi
if test -d schemas; then
    cp -R schemas/. "$bundle_dir/schemas/"
fi
if test -d examples; then
    (
        cd examples
        tar \
            --exclude='*/bin' \
            --exclude='*/bin/*' \
            --exclude='*/obj' \
            --exclude='*/obj/*' \
            -cf - .
    ) | (
        cd "$bundle_dir/examples"
        tar -xf -
    )
    if test -f "$bundle_dir/examples/ondc-preview/canonflow-evaluation.unsigned.json"; then
        cp \
            "$bundle_dir/examples/ondc-preview/canonflow-evaluation.unsigned.json" \
            "$bundle_dir/examples/ondc-preview/canonflow-evaluation.json"
        rm -f \
            "$bundle_dir/examples/ondc-preview/canonflow-evaluation.unsigned.json" \
            "$bundle_dir/examples/ondc-preview/test-receipt-private-key.hex"
    fi
fi
cp build-tools/verify-airgap.sh "$bundle_dir/verify-airgap.sh"
chmod 0755 "$bundle_dir/verify-airgap.sh"
printf '%s\n' "$image_name" > "$bundle_dir/image-reference.txt"

(
    cd "$bundle_dir"
    find . -type f -not -name checksums.sha256 -print0 \
        | sort -z \
        | xargs -0 sha256sum \
        | sed 's|  \\./|  |' > checksums.sha256
)

bundle_parent="$(cd "$(dirname -- "$bundle_dir")" && pwd)"
(
    cd "$bundle_parent"
    tar -czf "${bundle_base}.tar.gz" "$bundle_base"
)
printf 'Air-gap bundle: %s/%s.tar.gz\n' "$bundle_parent" "$bundle_base"
