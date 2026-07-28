# 2. Verification First; Constructive Modelling Dormant by Default

Date: 2026-07-28

## Status

Accepted

## Context

CanonFlow Foundation is a verification layer. Some regulated patterns may also
benefit from correct-by-construction projections, but generating a model does
not establish regulatory correctness, official certification, or total
semantic equivalence.

The permanent verification product and an optional constructive experiment
therefore need separate identities, activation rules and claims.

## Decision

Verification is always available as **Mode V**. It evaluates admitted rules
against supplied evidence and returns one of the four kernel verdicts. A
`Pass` is evidence-bounded: it means every applicable admitted rule in every
included assessment was evaluated with complete evidence and found conformant.
It is not an official certificate.

Constructive modelling is **Mode C** and is dormant by default. It may be
activated only for an explicitly admitted experimental profile with an
authoritative source, a differential oracle, mutation tests, provenance and a
stop decision. Mode V must neither depend on nor consume Mode C output.

Public evaluator and SDK surfaces use this claim vocabulary:

- `Verified` — evaluated against an identified admitted rule set and evidence
  boundary;
- `ConstructivelyProjected` — generated under an explicitly active admitted
  constructive profile;
- `Inconclusive` — the available evidence cannot establish the proposition;
- `Unsupported` — the installed evaluator or projector does not admit the
  pattern;
- `Experimental` — not a stable or authoritative production capability.

`Verified` and `ConstructivelyProjected` are deliberately distinct. Neither
means certified by ONDC, a regulator, a standards body, or any other external
authority.

The default kernel policy is `ConstructiveMode.Dormant`. No constructive
projection may be emitted under that default.

## Dependency Law

`CanonFlow.Assurance` owns the generic claim vocabulary and default mode. It
has no project references and must not depend on any regulated vertical,
FsAssay, reporting, CLI, Docker, database or constructive implementation.

`CanonFlow.Assurance.Xp` may depend only on `CanonFlow.Assurance`.

Vertical profiles may depend on the shared assurance contracts. The assurance
kernel must never depend on a vertical profile.

These laws and the prohibited public claim phrases are checked by
`build-tools/test-cm0-boundaries.sh` in CI.

## Consequences

- Empty assessment sets and zero-rule assessments cannot support `Pass`.
- Constructive experiments can stop or return to dormancy without weakening
  verification.
- Evidence reports state their assessed scope and are not described as
  certificates or mathematical proofs.
- External certification language requires separate written authority and is
  outside the repository's self-asserted claims.
