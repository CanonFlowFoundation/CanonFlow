#!/usr/bin/env python3
"""Verify the published CM1 schema retains its non-vacuity contract."""

import json
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
SCHEMA = ROOT / "docs" / "canonflow-obligation-manifest-v1.schema.json"


def require(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def main() -> int:
    errors: list[str] = []
    try:
        schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        print(f"CM1 schema failure: {error}", file=sys.stderr)
        return 1

    properties = schema.get("properties", {})
    definitions = schema.get("$defs", {})
    obligation = definitions.get("obligation", {}).get("properties", {})
    derivations = definitions.get("derivation", {}).get("oneOf", [])
    candidate = next(
        (
            item
            for item in derivations
            if item.get("properties", {}).get("kind", {}).get("const")
            == "Candidate"
        ),
        {},
    )

    require(
        properties.get("schemaVersion", {}).get("const") == "1.0",
        "schemaVersion must remain 1.0",
        errors,
    )
    require(
        properties.get("manifestType", {}).get("const")
        == "CanonFlowObligationManifest",
        "manifestType changed",
        errors,
    )
    require(
        properties.get("obligations", {}).get("minItems") == 1,
        "obligations must remain non-empty",
        errors,
    )
    require(
        obligation.get("requiredGates", {}).get("minItems") == 1,
        "requiredGates must remain non-empty",
        errors,
    )
    require(
        candidate.get("properties", {})
        .get("assumptionIds", {})
        .get("minItems")
        == 1,
        "candidate assumptionIds must remain non-empty",
        errors,
    )
    states = (
        obligation.get("projection", {})
        .get("properties", {})
        .get("state", {})
        .get("enum", [])
    )
    require(
        states
        == [
            "Dormant",
            "CandidateRequiringApproval",
            "Admitted",
            "Unsupported",
        ],
        "projection states changed or gained an unadmitted exact state",
        errors,
    )

    if errors:
        for error in errors:
            print(f"CM1 schema failure: {error}", file=sys.stderr)
        return 1
    print("CM1 obligation-manifest schema gate passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
