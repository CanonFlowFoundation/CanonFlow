#!/usr/bin/env bash
set -euo pipefail

image_name="${IMAGE_NAME:-canonflow-evaluator:0.1.0-alpha}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d /tmp/canonflow-container-gates-XXXXXX)"
trap 'rm -rf -- "$test_root"' EXIT

image_user="$(docker image inspect "$image_name" --format '{{.Config.User}}')"
if test -z "$image_user" || test "$image_user" = "0" || test "$image_user" = "root"; then
    echo "Container must declare a non-root runtime user." >&2
    exit 1
fi

container_id="$(docker create "$image_name")"
docker export --output "$test_root/rootfs.tar" "$container_id"
docker rm "$container_id" >/dev/null
if tar -tf "$test_root/rootfs.tar" \
    | grep -Eq '(^|/)(apt|apt-get|apk|bash|sh)$|private-key\\.hex$|\\.key$'; then
    echo "Runtime filesystem contains a shell, package manager, or private key." >&2
    exit 1
fi

set +e
docker run --rm --network none --read-only --entrypoint /bin/sh "$image_name" -c true >/dev/null 2>&1
shell_exit=$?
set -e
if test "$shell_exit" -eq 0; then
    echo "Chiseled runtime unexpectedly contains /bin/sh." >&2
    exit 1
fi

run_fixture() {
    fixture_name="$1"
    expected_exit="$2"
    output_name="$3"
    fsassay_path="${4:-/tools/fsassay}"
    output_dir="$test_root/$output_name"
    mkdir -p "$output_dir"
    chmod 0777 "$output_dir"

    set +e
    docker run --rm \
        --network none \
        --read-only \
        --cap-drop ALL \
        --security-opt no-new-privileges \
        --tmpfs /tmp:rw,noexec,nosuid,nodev,size=64m \
        --env "CANONFLOW_FSASSAY_PATH=$fsassay_path" \
        --mount "type=bind,src=$repository_root/examples/$fixture_name,dst=/input,readonly" \
        --mount "type=bind,src=$output_dir,dst=/output" \
        "$image_name" \
        evaluate --manifest /input/canonflow-evaluation.json --output /output
    actual_exit=$?
    set -e

    if test "$actual_exit" -ne "$expected_exit"; then
        echo "$fixture_name returned $actual_exit; expected $expected_exit." >&2
        exit 1
    fi
    for artifact in assessment.cff VERDICT.json REPORT.html EVIDENCE.md LOSS.md findings.sarif; do
        test -s "$output_dir/$artifact" || {
            echo "$fixture_name did not produce $artifact." >&2
            exit 1
        }
    done
}

run_constructive_fixture() {
    manifest_name="$1"
    expected_exit="$2"
    output_name="$3"
    output_dir="$test_root/$output_name"
    mkdir -p "$output_dir"
    chmod 0777 "$output_dir"

    set +e
    docker run --rm \
        --network none \
        --read-only \
        --cap-drop ALL \
        --security-opt no-new-privileges \
        --tmpfs /tmp:rw,noexec,nosuid,nodev,size=64m \
        --mount "type=bind,src=$repository_root/examples,dst=/input,readonly" \
        --mount "type=bind,src=$output_dir,dst=/output" \
        "$image_name" \
        evaluate \
        --manifest "/input/constructive-cm4/$manifest_name" \
        --output /output
    actual_exit=$?
    set -e

    if test "$actual_exit" -ne "$expected_exit"; then
        echo "$manifest_name returned $actual_exit; expected $expected_exit." >&2
        exit 1
    fi
    for artifact in assessment.cff VERDICT.json REPORT.html EVIDENCE.md LOSS.md findings.sarif; do
        test -s "$output_dir/$artifact" || {
            echo "$manifest_name did not produce $artifact." >&2
            exit 1
        }
    done
}

run_fixture fsassay-clean 0 pass-first
run_fixture fsassay-clean 0 pass-second
cmp "$test_root/pass-first/assessment.cff" "$test_root/pass-second/assessment.cff"

run_fixture fsassay-failing 1 fail
grep -q 'FSA-C02' "$test_root/fail/assessment.cff"

run_fixture fsassay-mixed 1 mixed
grep -q 'FSA-C02' "$test_root/mixed/assessment.cff"
grep -q '"kind":"ScannedFileCount".*"value":"2"' "$test_root/mixed/assessment.cff"

run_fixture ondc-preview 2 inconclusive
run_fixture fsassay-clean 3 tool-failure /missing/fsassay

run_constructive_fixture canonflow-evaluation.json 0 constructive-pass
grep -q '"assessments":\[\]' "$test_root/constructive-pass/assessment.cff"
grep -q '"constructiveAssessments":\[' "$test_root/constructive-pass/assessment.cff"
grep -q '"projectionState":"Admitted"' "$test_root/constructive-pass/assessment.cff"
grep -q '"evaluatedGates":4' "$test_root/constructive-pass/assessment.cff"

run_constructive_fixture canonflow-evaluation.fail.json 1 constructive-fail
grep -q '"verdict":"Fail"' "$test_root/constructive-fail/assessment.cff"

run_constructive_fixture canonflow-evaluation.missing.json 2 constructive-missing
grep -q '"evaluatedGates":0' "$test_root/constructive-missing/assessment.cff"
grep -q '"missingGateIds":\[' "$test_root/constructive-missing/assessment.cff"

docker run --rm \
    --pull=never \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --tmpfs /tmp:rw,noexec,nosuid,nodev,size=16m \
    -i \
    "$image_name" \
    receipt verify \
    --receipt - \
    --public-key-hex d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a \
    < "$test_root/constructive-pass/assessment.cff" \
    > "$test_root/constructive-verification.json"
grep -q '"valid":true' "$test_root/constructive-verification.json"

sdk_result="$test_root/ondc-sdk-result.json"
set +e
docker run --rm \
    --pull=never \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --pids-limit 128 \
    --memory 512m \
    --cpus 1 \
    --tmpfs /tmp:rw,noexec,nosuid,nodev,size=64m \
    -i \
    "$image_name" \
    ondc evaluate \
    --input - \
    --output - \
    --profile ondc-retail-1.2.0-preview \
    --instant 2026-07-27T10:30:00Z \
    < "$repository_root/examples/ondc-preview/evidence-bundle.json" \
    > "$sdk_result"
sdk_exit=$?
set -e
if test "$sdk_exit" -ne 2; then
    echo "ONDC SDK facade returned $sdk_exit; expected Inconclusive exit 2." >&2
    exit 1
fi

python3 - "$sdk_result" "$test_root/sdk-assessment.cff" <<'PY'
import json
import pathlib
import sys

result_path = pathlib.Path(sys.argv[1])
receipt_path = pathlib.Path(sys.argv[2])
value = json.loads(result_path.read_text(encoding="utf-8"))
assert value["schemaVersion"] == "1.0"
assert value["profile"] == "ondc-retail-1.2.0-preview"
assert value["result"]["verdict"] == "Inconclusive"
assert value["result"]["exitCode"] == 2
assert value["result"]["missingEvidence"]
receipt_path.write_text(value["receipt"], encoding="utf-8")
PY

docker run --rm \
    --pull=never \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --pids-limit 128 \
    --memory 512m \
    --cpus 1 \
    --tmpfs /tmp:rw,noexec,nosuid,nodev,size=64m \
    -i \
    "$image_name" \
    receipt verify \
    --receipt - \
    --allow-unsigned \
    < "$test_root/sdk-assessment.cff" \
    > "$test_root/sdk-verification.json"
grep -q '"valid":true' "$test_root/sdk-verification.json"

docker run --rm \
    --pull=never \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --pids-limit 128 \
    --memory 512m \
    --cpus 1 \
    --tmpfs /tmp:rw,noexec,nosuid,nodev,size=64m \
    "$image_name" \
    capabilities --json \
    > "$test_root/sdk-capabilities.json"
grep -q '"authority":"none"' "$test_root/sdk-capabilities.json"
grep -q '"status":"dormant"' "$test_root/sdk-capabilities.json"
grep -q '"productionEmission":false' "$test_root/sdk-capabilities.json"
grep -q '"ConstructivelyProjected"' "$test_root/sdk-capabilities.json"
grep -q '"manifestType":"CanonFlowObligationManifest"' "$test_root/sdk-capabilities.json"
grep -q '"schemaVersion":"1.0"' "$test_root/sdk-capabilities.json"
grep -q '"id":"required-contact-postgres-v1-lab"' "$test_root/sdk-capabilities.json"

set +e
docker run --rm \
    --pull=never \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --pids-limit 128 \
    --memory 512m \
    --cpus 1 \
    --tmpfs /tmp:rw,noexec,nosuid,nodev,size=64m \
    -i \
    "$image_name" \
    ondc evaluate \
    --input - \
    --output - \
    --profile ondc-retail-1.2.0-preview \
    --instant 2026-07-27T16:00:00+05:30 \
    < "$repository_root/examples/ondc-preview/evidence-bundle.json" \
    > "$test_root/sdk-invalid-instant.json"
invalid_instant_exit=$?
set -e
if test "$invalid_instant_exit" -ne 64; then
    echo "ONDC SDK invalid instant returned $invalid_instant_exit; expected usage exit 64." >&2
    exit 1
fi
grep -q '"code":"INVALID_INSTANT"' "$test_root/sdk-invalid-instant.json"

self_test_dir="$test_root/self-test"
mkdir -p "$self_test_dir"
chmod 0777 "$self_test_dir"
docker run --rm \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --tmpfs /tmp:rw,noexec,nosuid,nodev,size=16m \
    --mount "type=bind,src=$self_test_dir,dst=/output" \
    "$image_name" \
    self-test --output /output/self-test.json
grep -q '"passed": true' "$self_test_dir/self-test.json"

if grep -Eqi 'https?://|src=' "$test_root/pass-first/REPORT.html"; then
    echo "Offline HTML report contains a remote dependency." >&2
    exit 1
fi

printf 'Container gates passed for %s (user %s).\n' "$image_name" "$image_user"
