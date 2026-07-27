# 1. Ownership Law

Date: 2026-07-27

## Status

Accepted

## Context

To ensure clean dependency boundaries and proper accountability, the repository requires a strict ownership law mapping concepts to projects. 

## Decision

The following ownership law is established and mandated by the implementation spec:

```text
CanonFlow.Assurance
    owns generic verdict, evidence, assessment, canonical hashing,
    replay identity, receipt construction and receipt verification.

FsAssay
    owns F# engineering policy, static findings and release admission.

CanonFlow.Profile.ONDC
    owns ONDC parsing, typed traces, lifecycle evaluation, ONDC source
    mappings and the admitted ONDC rule pack.

CanonFlow.Evaluator
    orchestrates components without redefining their semantics.
```

### Dependency Rules
`CanonFlow.Assurance` <- `CanonFlow.Reports` <- `CanonFlow.Cli`
`CanonFlow.Assurance` <- `CanonFlow.Profile.ONDC`

`CanonFlow.Assurance` MUST NOT reference FsAssay, ONDCFlow, reporting, Docker, CLI, PostgreSQL, or XP concepts.

## Consequences
Any deviation from these strict ownership boundaries will fail the architectural constraints required for `CanonFlow.Evaluator` assessment and release.
