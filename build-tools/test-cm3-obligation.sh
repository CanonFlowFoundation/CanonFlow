#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
workspace_root="$(cd "$script_dir/../.." && pwd)"
canonflow_root="${CANONFLOW_ROOT:-$workspace_root/CanonFlow}"
fsassay_root="${FSASSAY_ROOT:-$workspace_root/FSharpAssay}"
profile="$canonflow_root/profiles/fsassay-canonflow-obligation-v1/profile.json"
admission="$canonflow_root/profiles/fsassay-canonflow-obligation-v1/admission.json"

test -f "$profile" || {
    echo "CM3 obligation profile not found at $profile." >&2
    exit 1
}
test -f "$fsassay_root/FsAssay.CanonFlow.Plugin/FsAssay.CanonFlow.Plugin.fsproj" || {
    echo "FsAssay CanonFlow plugin checkout not found at $fsassay_root." >&2
    exit 1
}
test "$(dotnet --version)" = "10.0.301" || {
    echo "The CM3 gate requires exactly .NET SDK 10.0.301." >&2
    exit 1
}

python3 - "$profile" "$admission" <<'PY'
import hashlib
import json
import pathlib
import sys

profile_path = pathlib.Path(sys.argv[1]).resolve()
admission_path = pathlib.Path(sys.argv[2]).resolve()
profile = json.loads(profile_path.read_text(encoding="utf-8"))
admission = json.loads(admission_path.read_text(encoding="utf-8"))

def digest(path):
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()

for path_key, digest_key in (
    ("sourcePath", "sourceDigest"),
    ("manifestPath", "manifestDigest"),
    ("generatedPath", "generatedDigest"),
    ("suppressionAuditPath", "suppressionAuditDigest"),
):
    target = (profile_path.parent / profile[path_key]).resolve()
    assert target.is_file(), target
    assert digest(target) == profile[digest_key], (path_key, digest(target), profile[digest_key])

manifest = json.loads((profile_path.parent / profile["manifestPath"]).read_text(encoding="utf-8"))
assert profile["obligationId"] in {item["id"] for item in manifest["obligations"]}
assert profile["semanticOracle"] is False
assert admission["claimBoundary"]["semanticOracle"] is False
assert admission["claimBoundary"]["businessOracleAgreement"] is False
assert digest(profile_path) == admission["scope"]["profileDigest"]
assert {rule["id"] for rule in admission["rules"]} == {
    "CFF-OBL001", "CFF-OBL002", "CFF-OBL003",
    "CFF-OBL004", "CFF-OBL005", "CFF-OBL006",
}
PY

dotnet restore "$fsassay_root/FsAssay.Tests/FsAssay.Tests.fsproj"
dotnet build "$fsassay_root/FsAssay.Tests/FsAssay.Tests.fsproj" \
    -c Release \
    --no-restore
dotnet run \
    --project "$fsassay_root/FsAssay.Tests/FsAssay.Tests.fsproj" \
    -c Release \
    --no-build \
    --no-restore \
    -- \
    --summary

runner="$fsassay_root/FsAssay.Runner/bin/Release/net10.0/FsAssay.Runner.dll"
plugin="$fsassay_root/FsAssay.CanonFlow.Plugin/bin/Release/net10.0/FsAssay.CanonFlow.Plugin.dll"
generated="$canonflow_root/examples/required-contact-lab-generated.fs"
test_root="$(mktemp -d /tmp/canonflow-cm3-XXXXXX)"
trap 'rm -rf -- "$test_root"' EXIT

set +e
CANONFLOW_FSASSAY_OBLIGATION_PROFILE="$profile" \
    dotnet "$runner" \
        --plugin "$plugin" \
        --out-json "$test_root/positive.json" \
        "$generated" \
        > "$test_root/positive.log" 2>&1
positive_exit=$?
set -e
if test "$positive_exit" -eq 3 || test "$positive_exit" -eq 64; then
    cat "$test_root/positive.log" >&2
    echo "The admitted CM3 artifact produced tool or invocation failure (exit $positive_exit)." >&2
    exit 1
fi
grep -q 'Loaded 1 CLI plugins' "$test_root/positive.log" || {
    cat "$test_root/positive.log" >&2
    echo "The admitted CM3 plugin was not loaded." >&2
    exit 1
}
python3 - "$test_root/positive.json" <<'PY'
import json
import sys

findings = json.load(open(sys.argv[1], encoding="utf-8"))
codes = {
    finding["code"]
    for file_result in findings
    for finding in file_result.get("violations", [])
}
assert not any(code.startswith("CFF-OBL") for code in codes), codes
PY

printf 'module Broken\nlet value =\n' > "$test_root/Broken.fs"
set +e
dotnet "$runner" --plugin "$plugin" "$test_root/Broken.fs" >/dev/null 2>&1
compiler_exit=$?
dotnet "$runner" --plugin "$test_root/missing-plugin.dll" "$generated" >/dev/null 2>&1
plugin_exit=$?
set -e
test "$compiler_exit" -eq 2 || {
    echo "Compiler failure returned $compiler_exit; expected Inconclusive exit 2." >&2
    exit 1
}
test "$plugin_exit" -eq 3 || {
    echo "Plugin load failure returned $plugin_exit; expected ToolFailure exit 3." >&2
    exit 1
}

printf 'CM3 obligation preservation gates passed.\n'
