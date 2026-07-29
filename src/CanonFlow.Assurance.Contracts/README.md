# CanonFlow.Assurance.Contracts

Pure .NET 10/F# contracts for content-addressed assurance definitions,
evidence, evaluator registration, rule packs, and receipts.

The package contains no ONDC-specific evaluator, I/O, network, signing,
orchestration, or mutable registry implementation.

Identifiers use validated smart constructors. Serializable obligation meaning
is kept separate from executable evaluator functions.
