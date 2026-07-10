# Ticket 1: Honest States - Upgrade Boolean Evaluation to Three-Valued Logic

## Context
In GSTFlow, we learned that treating engine outcomes as binary (`Pass`/`Fail` or `IsError = true/false`) forces the engine to guess or hallucinate when it encounters missing context (e.g., missing Place of Supply) or ambiguous rules (e.g., zero-tax items). We fixed this by moving to a 5-tier outcome system (`Pass | Warning | Unknown | NotSupported | Fail`).

## Action for CanonFlow
CanonFlow evaluates constraints through a Boolean lattice (`Canon.Core/Lattice.fs`), where `eval` currently returns `bool`. 
This is dangerous. If a constraint cannot be evaluated autonomously (e.g., missing context), it must yield an `Unknown` state rather than defaulting to `False` (Fail) or `True` (Pass).

We must upgrade `eval` in `Lattice.fs` to return a three-valued `LatticeOutcome = Satisfied | Violated | Unknown` and implement Kleene's strong logic of indeterminacy for `And`/`Or`/`Not`.
