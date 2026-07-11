# CanonFlow Governance

This project follows a **BDFL-Steward Model** (similar to the Python van Rossum posture).

1. **Direction:** You direct, decide, and document.
2. **ADRs as Memory:** All architectural decisions are recorded as ADRs.
3. **Open Source Promise:** Apache 2.0 forever. No monetization plans, no CLAs beyond DCO sign-off, no rugpulls.

## Contribution Ladder

- **Issues / Bug Reports:** Anyone can open an issue.
- **Driver Conformance PRs:** The safest way to contribute code is by adding a new database driver that passes the existing FsCheck driver conformance suite.
- **Driver Ownership:** Consistent contributors can take ownership of specific drivers.

## AI-Assisted Development Disclosure

Architecture and decisions are human-owned; generation is tooling. Every agent-generated commit is verified by the CI gates + human review. No agent holds main-branch write credentials.

## B8. The Kernel Promotion Rule

To keep CanonFlow pure, features and abstractions are promoted to the CanonFlow kernel ONLY when ALL THREE conditions are met:
1. It is obviously domain-neutral.
2. It has been implemented AND exercised in a real domain app (e.g. GSTFlow).
3. A second consumer can use it UNCHANGED (verified via EDIFlow or a similar design SPIKE).

### Promote/Wait Table
| Artifact | Status | Rationale |
| :--- | :--- | :--- |
| `RuleOutcome`, `Evidence`, `Envelope`, Serialization | **Wait for SPIKE** | Extract to `Canon.Verification` only when a second domain proves it. |
| GST text, RCM metadata, QR keys, backup ZIP | **NEVER** | Domain-owned. Must remain in GSTFlow. |
