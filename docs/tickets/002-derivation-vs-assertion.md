# Ticket 2: Derivation vs Assertion

## Context
In GSTFlow, the engine fell into the trap of projecting "false confidence" (e.g. silently inferring RCM instead of throwing it to a human-in-the-loop review). In autonomous systems, derivation of implicit facts must be strictly logged and separated from assertions.

## Action for CanonFlow
In `Canon.Core/Drift.fs`, `Fidelity` currently supports `Exact`, `Approximate`, and `Unsupported`. There is no way for the semantic optimizer or drift engine to state "I don't have enough context to compute this drift".
We must introduce `Unknown` to both `SemanticDriftStatus` and `Fidelity`. A workflow should never guess `LooseTarget` or `Approximate` if the AST comparison is genuinely opaque.
