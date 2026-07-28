#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
workspace_root="$(cd "$script_dir/../.." && pwd)"
canonflow_root="${CANONFLOW_ROOT:-$workspace_root/CanonFlow}"
ondcflow_root="${ONDCFLOW_ROOT:-$workspace_root/ONDCFlow}"

bash "$script_dir/build-integration.sh"

dotnet clean "$ondcflow_root/ONDCFlow.slnx" -c Release --nologo --verbosity quiet
dotnet clean "$canonflow_root/CanonFlow.slnx" -c Release --nologo --verbosity quiet

bash "$script_dir/build-integration.sh"

printf 'Cross-repository locked integration passed twice.\n'
