# Batch 2 — fsassay-clean relocation

`examples/fsassay-clean` was relocated to CanonFlowLabs because it has a
self-contained F#/.NET project closure: its only project reference is its own
`Clean.fsproj`, and its test packages are versioned and locked.

## Before and after graph

Before:

```text
CanonFlow.slnx
  └─ examples/fsassay-clean/tests/Clean.Tests.fsproj
       └─ ../Clean.fsproj
```

After:

```text
CanonFlow.slnx
  └─ no fsassay-clean project or CanonFlowLabs reference

CanonFlowLabs.slnx
  └─ experiments/fsassay-clean/tests/Clean.Tests.fsproj
       └─ ../Clean.fsproj
```

The container hardening gate retains its deterministic positive receipt check
using the existing `constructive-cm4` fixture. Its former `fsassay-clean`
fixture references were removed with the relocated sample, so CanonFlow's test
scripts do not acquire a CanonFlowLabs path dependency.

## Recovery and rollback

The original content remains recoverable from CanonFlow commit
`7467073049b96e3b2d86e49fa9b114e6920ec100`, path
`examples/fsassay-clean/`. CanonFlowLabs records each original blob identity in
`experiments/fsassay-clean/README.md`.

Rollback is the paired-commit revert: revert the CanonFlowLabs relocation
commit, then revert this CanonFlow removal commit. No retained CanonFlow
project references CanonFlowLabs.
