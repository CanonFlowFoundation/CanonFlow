#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
workspace_root="$(cd "$script_dir/../.." && pwd)"
canonflow_root="${CANONFLOW_ROOT:-$workspace_root/CanonFlow}"
ondcflow_root="${ONDCFLOW_ROOT:-$workspace_root/ONDCFlow}"

test -f "$canonflow_root/CanonFlow.slnx" || {
    echo "CanonFlow checkout not found at $canonflow_root." >&2
    exit 1
}
test -f "$ondcflow_root/ONDCFlow.slnx" || {
    echo "ONDCFlow checkout not found at $ondcflow_root." >&2
    exit 1
}
test "$(dotnet --version)" = "10.0.301" || {
    echo "The integration gate requires exactly .NET SDK 10.0.301." >&2
    exit 1
}

(
    cd "$ondcflow_root"
    dotnet restore ONDCFlow.slnx --locked-mode
    dotnet build ONDCFlow.slnx -c Release --no-restore
    dotnet test ONDCFlow.slnx -c Release --no-build --no-restore
)

(
    cd "$canonflow_root"
    dotnet restore CanonFlow.slnx --locked-mode
    dotnet build CanonFlow.slnx -c Release --no-restore
    dotnet test CanonFlow.slnx -c Release --no-build --no-restore
)

printf 'Cross-repository locked integration passed.\n'
