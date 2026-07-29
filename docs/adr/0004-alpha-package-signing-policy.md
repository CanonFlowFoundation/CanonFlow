# ADR 0004: Alpha package signing policy

- Status: Accepted for `0.1.0-alpha.1`
- Date: 2026-07-29
- Scope: `CanonFlow.Assurance.Contracts`

## Decision

`CanonFlow.Assurance.Contracts 0.1.0-alpha.1` may be released unsigned only when every control below is satisfied:

1. The package has an exact recorded SHA-256 digest.
2. Consumers restore in locked mode from the exact package version.
3. NuGet repository metadata identifies the merged and tagged source commit.
4. Two clean builds from that committed revision produce identical canonical package contents and SHA-256.
5. The package digest is bound into evaluator identity and the resulting receipt.
6. `NU3004` is disclosed in release evidence as the expected consequence of the unsigned-alpha decision.

This permission is limited to the alpha release. An external paid pilot or stable release requires a signed package and successful signature verification. It cannot inherit the alpha exception.

## Consequences

- `NU3004` is a disclosed release condition, not an ignored warning.
- The alpha package must not be published from an uncommitted tree or an unmerged feature branch.
- A digest match does not replace commit identity, locked restore, reproducibility, or receipt binding; all controls are conjunctive.
- Failure of any control blocks publication.

## Release boundary

This ADR authorizes policy only. It does not authorize a commit, tag, package publication, push, merge, or release.
