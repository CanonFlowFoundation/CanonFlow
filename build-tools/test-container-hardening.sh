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
