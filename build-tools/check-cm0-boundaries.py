#!/usr/bin/env python3
"""CM0 dependency and public-claim gates."""

from pathlib import Path
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]


def project_references(relative_path: str) -> list[str]:
    project = ROOT / relative_path
    document = ET.parse(project)
    return [
        element.attrib["Include"].replace("\\", "/")
        for element in document.iter()
        if element.tag.endswith("ProjectReference")
    ]


def check_dependencies(errors: list[str]) -> None:
    assurance = project_references(
        "src/CanonFlow.Assurance/CanonFlow.Assurance.fsproj"
    )
    if assurance:
        errors.append(
            "CanonFlow.Assurance must have no ProjectReference; found "
            + ", ".join(assurance)
        )

    xp = project_references(
        "src/CanonFlow.Assurance.Xp/CanonFlow.Assurance.Xp.fsproj"
    )
    allowed = {"../CanonFlow.Assurance/CanonFlow.Assurance.fsproj"}
    unexpected = sorted(set(xp) - allowed)
    if unexpected:
        errors.append(
            "CanonFlow.Assurance.Xp may reference only CanonFlow.Assurance; found "
            + ", ".join(unexpected)
        )


def source_files() -> list[Path]:
    skipped = {".git", "bin", "node_modules", "obj"}
    files = [ROOT / "README.md"]
    for path in (ROOT / "src").rglob("*"):
        if path.is_file() and not any(part in skipped for part in path.parts):
            files.append(path)
    return files


def check_public_claims(errors: list[str]) -> None:
    prohibited = {
        "status:** certified": "self-awarded certification",
        "signed certification generated": "self-awarded certification",
        "100% exact mathematical fidelity": "unbounded exactness",
        "mathematically guaranteed": "unbounded guarantee",
        "this document certifies the semantic translation fidelity": "certificate claim",
        "full semantic coverage": "unbounded coverage",
        "all database constraints are safely bounded": "unbounded coverage",
        "mathematical proof of the schema": "proof claim",
        "=== mathematical proof ===": "proof claim",
    }
    for path in source_files():
        try:
            text = path.read_text(encoding="utf-8").casefold()
        except UnicodeDecodeError:
            continue
        for phrase, reason in prohibited.items():
            if phrase in text:
                errors.append(
                    f"{path.relative_to(ROOT)} contains prohibited {reason}: {phrase!r}"
                )


def main() -> int:
    errors: list[str] = []
    check_dependencies(errors)
    check_public_claims(errors)
    if errors:
        for error in errors:
            print(f"CM0 boundary failure: {error}", file=sys.stderr)
        return 1
    print("CM0 dependency and public-claim gates passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
