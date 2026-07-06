# ADR-015: Semantic Optimizer (Subsumption and Contradiction)

## Context
CanonFlow's `Lattice` AST faithfully parses database rules, but raw database schemas often contain redundant or overlapping constraints (e.g., `CHECK (age > 18) AND CHECK (age > 21)`). Furthermore, bad schema migrations might introduce unsatisfiable contradictions (e.g., `CHECK (age > 50) AND CHECK (age < 30)`). If transpiled directly, these inefficiencies bloat the generated code and confuse AI agents.

## Decision
We will implement a `simplify` pass in the `Lattice` module. This pass will perform:
1. **Duplicate-Leaf Idempotence**: Reducing `And(x, x)` to `x` and `Or(x, x)` to `x`.
2. **Interval Intersection (Meet)**: Resolving multiple `Range` constraints on the same field into a single canonical bound.
3. **Contradiction Detection**: Evaluating empty interval intersections and collapsing them directly to `False`.

The optimizer will act strictly within the rules of a boolean algebra.

## Consequences
- The generated TypeScript and OpenAPI schemas will be vastly more compact and readable.
- If an intersection resolves to `False`, the CanonFlow CLI will be able to emit a compile-time contradiction diagnostic to the user, blocking agents from interacting with mathematically impossible forms.
- The `simplify` function will be rigorously proven via FsCheck to preserve semantics (`equivalent (simplify l) l`) and guarantee idempotency (`simplify (simplify l) = simplify l`).
