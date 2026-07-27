Use the following as the recovery constitution for CanonFlow × FsAssay × ONDCFlow. It converts the review findings into mathematical, testable requirements.

# CanonFlow × ONDCFlow Recovery Constitution

## 1. System definition

Let:

[
\mathcal S=
\langle C,F,O,P,E,T,R,D\rangle
]

where:

| Symbol | Meaning                             |
| ------ | ----------------------------------- |
| (C)    | CanonFlow assurance kernel          |
| (F)    | FsAssay engineering judge           |
| (O)    | ONDCFlow domain assessor            |
| (P)    | CanonFlow.Assurance package         |
| (E)    | Evaluator and execution environment |
| (T)    | Tests and evidence                  |
| (R)    | Deterministic `.cff` receipt        |
| (D)    | Documentation and milestone claims  |

The ownership law is:

[
C \cap F \cap O = \varnothing
]

with dependencies:

[
O \rightarrow C
\qquad
F \rightarrow \text{source code}
\qquad
C \not\rightarrow O
]

Therefore:

* CanonFlow owns verdicts, evidence, digests, receipts and sealing.
* FsAssay owns engineering-policy analysis.
* ONDCFlow owns ONDC sources, traces, lifecycle and protocol rules.
* CanonFlow must know nothing about Beckn or ONDC.

---

# 2. Epistemic law

## R-01: No evidence means no Pass

For every requirement (r):

[
\operatorname{Pass}(r)
\iff
\operatorname{Applicable}(r)
\land
\operatorname{EvidenceComplete}(r)
\land
\operatorname{EvaluatorHealthy}(r)
\land
\operatorname{Conformant}(r)
]

Otherwise:

[
\neg\operatorname{EvidenceComplete}(r)
\Rightarrow
\operatorname{Inconclusive}
]

[
\neg\operatorname{EvaluatorHealthy}(r)
\Rightarrow
\operatorname{ToolFailure}
]

[
\operatorname{EvidenceComplete}(r)
\land
\neg\operatorname{Conformant}(r)
\Rightarrow
\operatorname{Fail}
]

## R-02: Health and compliance remain independent

```fsharp
type Health =
    | Complete
    | Incomplete
    | Broken

type Compliance =
    | Conformant
    | NonConformant
    | Undetermined
```

Required mapping:

| Verdict        | Health       | Compliance      |
| -------------- | ------------ | --------------- |
| `Pass`         | `Complete`   | `Conformant`    |
| `Fail`         | `Complete`   | `NonConformant` |
| `Inconclusive` | `Incomplete` | `Undetermined`  |
| `ToolFailure`  | `Broken`     | `Undetermined`  |

The following mappings are forbidden:

[
\operatorname{ToolFailure}\mapsto\operatorname{NonConformant}
]

[
\operatorname{Inconclusive}\mapsto\operatorname{Conformant}
]

---

# 3. Claim integrity

For milestone (m), define:

[
\operatorname{MayClaimComplete}(m)=
B_m\land T_m\land N_m\land E_m\land C_m
]

where:

* (B_m): clean build succeeds.
* (T_m): positive tests pass.
* (N_m): negative tests demonstrate detection.
* (E_m): evidence is archived.
* (C_m): CI independently reproduces the result.

Therefore:

[
\neg\operatorname{MayClaimComplete}(m)
\Rightarrow
\operatorname{Status}(m)\in
{\text{Proposed},\text{Experimental},\text{Partial}}
]

“Operational”, “certified”, “verified” and “complete” are forbidden until the predicate is true.

Immediately:

* Disable the simulated certification workflow.
* Rename M7–M12 to `Experimental`.
* Remove hard-coded successful verdicts.
* Remove empty or touched “certificate” artifacts.

---

# 4. Work-state machine

Every implementation story must follow:

```fsharp
type WorkState =
    | Proposed
    | Admitted of AdmissionEvidence
    | RedWitnessed of RedEvidence
    | Implemented of ChangeEvidence
    | GreenWitnessed of GreenEvidence
    | Refactored of RefactorEvidence
    | Reviewed of ReviewEvidence
    | Sealed of Receipt
```

Allowed transitions:

[
\text{Proposed}
\rightarrow
\text{Admitted}
\rightarrow
\text{RedWitnessed}
\rightarrow
\text{Implemented}
\rightarrow
\text{GreenWitnessed}
\rightarrow
\text{Refactored}
\rightarrow
\text{Reviewed}
\rightarrow
\text{Sealed}
]

No transition may be skipped:

[
\operatorname{transition}(s_i,s_j)
\Rightarrow j=i+1
]

Every requirement (r_i) must have:

[
r_i\rightarrow
\langle
t_i^{-},
t_i^{+},
e_i,
c_i
\rangle
]

where:

* (t_i^{-}): failing test before implementation.
* (t_i^{+}): passing test after implementation.
* (e_i): produced evidence.
* (c_i): commit digest.

---

# 5. Reproducible .NET 10 build

## R-10: Clean-clone build law

For repository (x) and supported Linux environment (e):

[
\operatorname{Build}(x,e)=1
]

must hold without uncommitted files or manual preparation.

Required commands:

```bash
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

## R-11: Portable paths

For every MSBuild path (p):

[
p=\operatorname{PathCombine}(p_1,\ldots,p_n)
]

Hard-coded `\` or `/` must not be used as an assumed platform separator in MSBuild execution paths.

## R-12: Unique package versions

For package identifier (p):

[
|\operatorname{CentralVersions}(p)|=1
]

Therefore CanonFlow must contain exactly one Npgsql version.

FSharp.Core must satisfy every dependency:

[
v_{\text{FSharp.Core}}
\ge
\max(v_{\text{dependency minimums}})
]

`packages.lock.json` must be committed.

## R-13: Consumable assurance package

The integration sequence must be:

[
\operatorname{Pack}(C)\rightarrow P
]

[
\operatorname{Restore}(O,P)=1
]

A clean process shall:

1. Build and pack `CanonFlow.Assurance`.
2. Copy the package into a generated offline feed.
3. Restore ONDCFlow from that feed.
4. Build and test ONDCFlow.

An absent `local-feed` must never be a hidden prerequisite.

---

# 6. CanonFlow evaluator truth

There must be exactly one evaluation function:

```fsharp
evaluate:
    EvaluationManifest
    -> Async<AssessmentReceipt>
```

Every interface must be a projection of this result:

[
\operatorname{CLI}(x)
=====================

# \operatorname{JSON}(x)

# \operatorname{SARIF}(x)

# \operatorname{HTML}(x)

\pi(\operatorname{Evaluate}(x))
]

No report generator may construct its own verdict.

## R-20: No synthetic success

For every production path (p):

[
\neg\operatorname{Contains}
(p,{\text{stub},\text{simulated success},\text{hard-coded Conformant}})
]

## R-21: Exit-code algebra

| Result             | Exit code |
| ------------------ | --------: |
| Pass               |       `0` |
| Fail               |       `2` |
| Inconclusive       |       `3` |
| ToolFailure        |       `4` |
| Invalid invocation |      `64` |

[
\operatorname{ExitCode}
=======================

f(\operatorname{Verdict})
]

It must never be inferred from the presence of an output file.

---

# 7. ONDC source-lock admission

Define:

```fsharp
type SourceDocument = {
    Id: SourceId
    Version: SourceVersion
    EffectiveFrom: DateOnly option
    Digest: Digest
    Licence: string
}

type SourceLock = {
    Protocol: string
    ProtocolVersion: string
    Documents: NonEmptyList<SourceDocument>
    Precedence: SourceId list
    ConflictDecisions: ConflictDecision list
    RulePackDigest: Digest
    Reviewer: string
}
```

The lock digest is:

[
d_s=
\operatorname{SHA256}
\left(
\operatorname{canon}(\operatorname{SourceLock})
\right)
]

Assessment is allowed only when:

[
\operatorname{VerifySourceLock}(s,d_s)=1
]

Therefore:

[
\neg\operatorname{VerifySourceLock}(s,d_s)
\Rightarrow
\operatorname{Inconclusive}
]

The assessor must consume the verified lock:

```fsharp
assess:
    VerifiedSourceLock
    -> EvidenceBundle
    -> AssessmentReceipt
```

It must be impossible to call `assess` with an unverified source lock.

---

# 8. Evidence schema/parser equivalence

Let:

* (V(b)): evidence bundle (b) passes JSON Schema.
* (P(b)): parser returns `Ok`.

Required law:

[
\forall b,\quad V(b)\Rightarrow P(b)
]

The reverse structural law is:

[
P(b)\Rightarrow
\operatorname{StructurallyValid}(b)
]

Add generated and mutation tests for:

* Missing context.
* Missing payload.
* Wrong action.
* Excessive JSON depth.
* Oversized bundle.
* Invalid UTF-8.
* Duplicate object properties.
* Unknown required protocol version.

The schema and parser must share one representation. Do not independently invent two shapes.

---

# 9. Exact lifecycle model

For the MVP order-formation profile:

[
L=
[
\text{search},
\text{on_search},
\text{select},
\text{on_select},
\text{init},
\text{on_init},
\text{confirm},
\text{on_confirm}
]
]

Full evaluation requires:

[
\operatorname{ObservedActions}=L
]

Partial evaluation may accept a proper prefix:

[
\operatorname{ObservedActions}
\prec L
]

but its result must be:

[
\operatorname{PartialTrace}
\Rightarrow
\operatorname{Inconclusive}
]

Not `Pass`.

The following must fail:

[
[]
]

[
[\text{confirm}]
]

[
[\text{search},\text{confirm}]
]

[
[\text{search},\text{search},\ldots]
]

[
L+\left[\text{on_confirm}\right]
]

Implement lifecycle as a total transition function:

```fsharp
transition:
    LifecycleState
    -> BecknAction
    -> Result<LifecycleState, LifecycleFinding>
```

---

# 10. Rule-pack mathematics

Each rule must contain:

```fsharp
type Rule = {
    Id: RuleId
    Source: SourceReference
    Applicability: ApplicabilityPredicate
    Evaluate: VerifiedTrace -> RuleResult
    PositiveVectors: VectorId list
    NegativeVectors: VectorId list
}
```

For rule (r):

[
\operatorname{Admitted}(r)
\iff
\operatorname{SourceExists}(r)
\land
\operatorname{PositiveVectorExists}(r)
\land
\operatorname{NegativeVectorExists}(r)
]

Rule coverage must be reported honestly:

[
\operatorname{Coverage}
=======================

\frac{|\operatorname{ImplementedRules}|}
{|\operatorname{AdmittedRules}|}
]

The system must not claim “48 rules” until:

[
|\operatorname{ImplementedRules}|=48
]

For the first vertical slice, implement only:

1. Evidence-schema validity.
2. Context completeness.
3. Identifier grammar.
4. Transaction-ID consistency.
5. Exact lifecycle ordering.
6. Quote consistency.

Six proven rules are more valuable than 48 named rules.

---

# 11. Deterministic receipt and sealing

For assessment (a) and fixed evaluation context (e):

[
R_1=\operatorname{Evaluate}(a,e)
]

[
R_2=\operatorname{Evaluate}(a,e)
]

Required:

[
\operatorname{bytes}(R_1)=\operatorname{bytes}(R_2)
]

Unsigned receipt digest:

[
d_r=
\operatorname{SHA256}
\left(
\operatorname{canon}
\left(
R\setminus\operatorname{Seal}
\right)
\right)
]

Signature:

[
\sigma=
\operatorname{Ed25519.Sign}(sk,d_r)
]

Verification:

[
\operatorname{Verify}(pk,R)=
\operatorname{Ed25519.Verify}(pk,d_r,\sigma)
]

## Tamper law

Let (\Pi(R)) be all mutable JSON pointers in the receipt.

[
\forall p\in\Pi(R),\quad
\operatorname{Verify}
\left(
pk,\operatorname{Mutate}(R,p)
\right)=0
]

The JSON-pointer generator must be derived from the serialized receipt structure, not from a manually maintained field list.

---

# 12. FsAssay mandate

Until AST and symbol analysis exist, rename the current script:

```text
FsAssay-ONDC
    ↓
ONDC Lexical Policy Sentinel
```

A lexical scanner must not be presented as semantic proof.

For rule (f_i):

[
\operatorname{Enforced}(f_i)
\iff
t_i^{-}=1
\land
t_i^{+}=1
\land
\operatorname{CIInvokes}(f_i)
]

Here (t_i^{-}=1) means the negative fixture was correctly rejected.

Inline bypasses are forbidden. A waiver must be structured:

```fsharp
type Waiver = {
    RuleId: RuleId
    FileDigest: Digest
    Reason: NonEmptyString
    Owner: NonEmptyString
    ExpiresAt: DateOnly
}
```

[
\operatorname{WaiverValid}(w)
\iff
\operatorname{DigestMatches}(w)
\land
\operatorname{Today}\le w.ExpiresAt
]

---

# 13. Release gates

Define:

[
G=
{G_0,G_1,\ldots,G_9}
]

| Gate  | Requirement                           |
| ----- | ------------------------------------- |
| (G_0) | Clean restore/build on Linux .NET 10  |
| (G_1) | CanonFlow unit and property tests     |
| (G_2) | ONDC golden and negative vectors      |
| (G_3) | FsAssay negative sentinel             |
| (G_4) | Source-lock verification              |
| (G_5) | Receipt determinism                   |
| (G_6) | Exhaustive receipt tamper tests       |
| (G_7) | Offline container execution           |
| (G_8) | CLI/JSON/SARIF/HTML verdict agreement |
| (G_9) | Documentation claim audit             |

Release predicate:

[
\operatorname{Releaseable}
\iff
\bigwedge_{i=0}^{9}G_i
]

If any gate is `Inconclusive` or `ToolFailure`:

[
\operatorname{Releaseable}=0
]

A failed gate blocks release but must retain its correct epistemic classification.

---

# 14. Air-gap law

Normal assessment must satisfy:

[
\operatorname{NetworkBytesSent}=0
]

The container must use:

* Exact .NET 10 GA image digest.
* Non-root user.
* Read-only root filesystem.
* Dropped Linux capabilities.
* `--network none`.
* Locked packages.
* Mandatory SBOM.
* Artifact manifest with SHA-256 digests.
* No PostgreSQL dependency for the ONDC MVP.

No `latest`, preview image, optional SBOM or runtime package download is permitted.

---

# 15. Milestone admission conditions

| Milestone              | May start only when                                      |
| ---------------------- | -------------------------------------------------------- |
| ONDC M7 source lock    | Clean build is green                                     |
| ONDC M8 order assessor | Source lock is verified and consumed                     |
| Docker M6              | CLI produces a real receipt                              |
| PostgreSQL M9          | Core evaluator is already deterministic                  |
| WASM M10               | Native receipt vectors exist                             |
| Viewer M11             | Receipt verification API is complete                     |
| Certification M12      | External verifier, governance key and tamper tests exist |

For native/WASM parity:

[
\forall v\in\operatorname{GoldenVectors},
\quad
\operatorname{Receipt}_{native}(v)
==================================

\operatorname{Receipt}_{wasm}(v)
]

For certification:

[
\operatorname{Certified}(R)
\Rightarrow
\operatorname{EvaluatorExecuted}(R)
\land
\operatorname{AllGatesPassed}(R)
\land
\operatorname{ExternalKeySigned}(R)
]

Creating an empty file can never satisfy any certification predicate.

---

# Final stopping rule

Do not add the next milestone merely because the current code compiles.

Advance only when:

[
\text{Red witnessed}
\land
\text{Green witnessed}
\land
\text{Negative case caught}
\land
\text{Receipt produced}
\land
\text{CI reproduced}
]

The immediate objective is therefore:

[
\boxed{
\text{Clean Build}
\rightarrow
\text{Verified Source Lock}
\rightarrow
\text{Exact Trace}
\rightarrow
\text{Six Rules}
\rightarrow
\text{Honest Verdict}
\rightarrow
\text{Deterministic Receipt}
}
]

That single proven chain will recover more credibility than completing another six named milestones.
