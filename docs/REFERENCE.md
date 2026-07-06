# CanonFlow Technical Reference

## The Core Algebra: `Lattice<'Leaf>`
Defined in `Canon.Core.Lattice`.

CanonFlow maps all logic to a six-constructor mathematical lattice:
- `True`
- `False`
- `Leaf` (a `Constraint`)
- `Not`
- `And`
- `Or`

Because of the algebraic properties defined via `FsCheck`, `Lattice.fs` is guaranteed to obey De Morgan's Laws, Involution, and Idempotence.

## Constraint Fidelity: `Lineage.fs`

When transpiling a `Lattice` to a target language, CanonFlow grades the operation using `Fidelity`:
- `Fidelity.Exact`: 100% byte-for-byte semantic match.
- `Fidelity.Approximate(reason)`: The target language doesn't support the full precision of the database rule.
- `Fidelity.Unsupported(reason)`: The database rule is impossible to enforce in the target system.

## The Drift Engine: `Drift.fs`

Analyzes combinations of `Fidelity` reports across different systems (e.g., TypeScript vs OpenAPI) to output a `DriftViolation` identifying exactly where the frontend validation is hallucinating or missing constraints compared to the database.
