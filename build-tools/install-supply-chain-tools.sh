#!/usr/bin/env bash
set -euo pipefail

install_dir="${1:-/tmp/canonflow-supply-chain-tools}"
mkdir -p "$install_dir"

syft_version="1.44.0"
syft_archive="syft_${syft_version}_linux_amd64.tar.gz"
syft_sha256="0e91737aee2b5baf1d255b959630194a302335d848ff97bb07921eb6205b5f5a"
curl -fsSL \
    -o "$install_dir/$syft_archive" \
    "https://github.com/anchore/syft/releases/download/v${syft_version}/$syft_archive"
printf '%s  %s\n' "$syft_sha256" "$install_dir/$syft_archive" | sha256sum -c -
tar -xzf "$install_dir/$syft_archive" -C "$install_dir" syft

cosign_version="2.6.4"
cosign_sha256="309779b0c4e409186b0a80daba99041fe2cf65a920ce645013901df6211895a9"
curl -fsSL \
    -o "$install_dir/cosign" \
    "https://github.com/sigstore/cosign/releases/download/v${cosign_version}/cosign-linux-amd64"
printf '%s  %s\n' "$cosign_sha256" "$install_dir/cosign" | sha256sum -c -
chmod 0755 "$install_dir/syft" "$install_dir/cosign"

printf 'Installed Syft %s and Cosign %s in %s.\n' \
    "$syft_version" "$cosign_version" "$install_dir"
