# 3. Canonical Obligation and Projection Contract

Date: 2026-07-28

## Status

Accepted

## Context

Regulated profiles need a shared way to identify obligations, bind them to
authoritative inputs, describe projection status and name the gates required to
establish a projection. That contract must not synthesize domain rules or allow
empty evidence to become success.

## Decision

`CanonFlow.Assurance` owns a pure, vertical-neutral obligation contract.

An obligation contains:

- a canonical structured identifier;
- the SHA-256 digest of its authoritative source;
- the SHA-256 digest of its normalized executable predicate;
- one or more versioned proof-gate references bound to implementation digests;
- one structured projection derivation.

Projection derivations are closed:

- `None` maps to `Dormant`;
- `Candidate` requires one or more structured assumption identifiers and maps
  to `CandidateRequiringApproval`;
- `Admitted` requires a structured admission identifier;
- `Unsupported` requires a structured reason identifier.

Candidate, dormant and unsupported derivations always evaluate to
`Inconclusive`. An admitted derivation evaluates to `Pass` only when every
required gate has a `Pass` result. Missing or empty gate results are
`Inconclusive`; observed `Fail` and `ToolFailure` retain their normal verdict
severity.

The versioned wire representation is
`CanonFlowObligationManifest` schema `1.0`. It contains a non-empty obligation
array, a policy digest and a derived `protectedDigest`. The protected digest is
SHA-256 over the canonical manifest payload excluding the protected digest
field itself. Obligations and gates are sorted by canonical identifier before
serialization.

Parsing accepts bytes, uses strict UTF-8, rejects duplicate, missing and unknown
fields, validates all identifiers and digests, verifies the protected digest,
and finally requires byte-for-byte canonical form.

Only identifiers, digests, enum cases and gate verdicts participate in
executable decisions. Descriptions and other prose are intentionally absent
from the manifest schema.

The normative verdict/exit mapping is:

| Verdict | Exit code |
|---|---:|
| `Pass` | 0 |
| `Fail` | 1 |
| `Inconclusive` | 2 |
| `ToolFailure` | 3 |

Exit code `64` means invalid invocation and has no verdict inverse.

## Consequences

- Empty obligation and proof-gate lists are unrepresentable after parsing or
  construction.
- A candidate derivation cannot be serialized or evaluated as an admitted
  projection.
- Policy, source, predicate, gate, assumption and projection changes alter the
  protected digest.
- The contract activates no constructive generator and contains no vertical
  semantics.
