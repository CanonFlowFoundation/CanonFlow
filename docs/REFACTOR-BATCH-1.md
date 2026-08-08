# Surgical refactor batch 1: isolated Assurance.Xp experiment

Base: `ae89cd7deca60d3bfa6146c460a417686b0754d3`

This batch moves only `CanonFlow.Assurance.Xp` and its tests to the separate
CanonFlowLabs Draft PR. It does not alter database/search/event/code-generation
projects, ONDC/GST policy, public package behavior, or CanonFlow core logic.

## Retained and moved manifest

| Item | Before | After | Destination | Recovery |
|---|---|---|---|---|
| `src/CanonFlow.Assurance.Xp` | CanonFlow solution/project | removed from CanonFlow solution | CanonFlowLabs `experiments/CanonFlow.Assurance.Xp` | restore from base commit or Labs move PR |
| `tests/CanonFlow.Assurance.Xp.Tests` | CanonFlow test solution | removed from CanonFlow solution | CanonFlowLabs `experiments/CanonFlow.Assurance.Xp.Tests` | restore from base commit or Labs move PR |
| all other projects | unchanged | unchanged | retain or parked per inventory | base commit |

The Labs destination records the original paths, source commit, reason, and
recovery path in `MOVED-FROM-CANONFLOW.md`. CanonFlow contains no reference to
CanonFlowLabs.

## Project graph

Before: `CanonFlow.slnx` contained 23 project entries, including the two XP
projects. After: it contains 21 entries; the two XP projects are absent. The
remaining graph is otherwise byte-for-byte unchanged.

The complete before graph is the `CanonFlow.slnx` file at the base commit. The
after graph is the current `CanonFlow.slnx`; the only changes are the two
deleted project entries.

## Safety claims

- No source history was deleted; the source commit remains recoverable.
- No retained project references `CanonFlowLabs`.
- No database/search/event/code-generation project was moved or deleted.
- No ONDC/GST policy was changed.
- This batch is reversible by restoring the two original directories and the
  two solution entries.
