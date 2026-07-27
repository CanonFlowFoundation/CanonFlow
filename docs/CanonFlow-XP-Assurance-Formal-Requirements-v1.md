# CanonFlow XP Assurance — Formal Requirement Specification

## The witness is not the claim. The signature is not the source. The empty proof is not a proof.

**Document class:** Normative requirement algebra (independent recompilation of the source enhancement spec)
**Source under review:** `CanonFlow XP Assurance — Mathematical Enhancement Requirements for Enforceable AI-Assisted XP`, v0.1.0, 2026-07-27
**Compilation stance:** Independent, with explicit cherry-pick ledger (§14) and defect register (§15)
**Numbering:** my requirements are `XR-n`; my discharges are `XP-n`; the source's are cited as `R-XP-n`

> The source is the strongest of the three CanonFlow specifications reviewed so
> far. Its RED decomposition (`R-XP-011`) and its seal-verdict orthogonality
> (`R-XP-006`) are original and correct. This document keeps those, promotes two
> principles to type constraints, and repairs the places where the stated law
> does not survive an adversarial agent — which is the only agent the document
> claims to defend against.
>
> Two findings dominate. First, the same **vacuous-`Pass`** defect that appeared
> in the ONDCFlow reducer recurs here in two new sites. Second — and this is new
> to this document because it is the first with an untrusted agent in the loop —
> **the agent boundary is asserted in prose but not mechanized in the types.** A
> system whose thesis is `AgentClaim ⇏ TrustedEvidence` must not rest that thesis
> on a filesystem permission and a naming convention.

---

## 0. Divergence Policy

Let \(S\) be the source and \(R\) this requirement set.

\[
R \vdash \varphi \ \wedge\ S \vdash \neg\varphi
\;\Longrightarrow\;
\text{(a) } \varphi \text{ carries } \textbf{[DIV]}, \text{ and (b) } \S15 \text{ records the defect.}
\]

A guard that names a predicate it cannot compute is not a guard. A requirement
with no discharge (§16) is a wish. Neither is counted in coverage.

---

## 1. Notation

| Symbol | Meaning |
|---|---|
| \(A \to B\) | total function |
| \(A \rightharpoonup B\) | partial function (**prohibited** in the verification kernel) |
| \(\mathrm{NE}(A)\) | non-empty list |
| \(H(\cdot)\) | SHA-256 |
| \(\mathcal J(\cdot)\) | RFC 8785 canonicalization |
| \(\parallel\) | byte concatenation — **only** under an unambiguous framing (XR-19) |
| \(\sqcup,\ \sqsubseteq\) | verdict join, verdict severity order |
| \(\preceq_{\mathrm{ev}}\) | evidence containment order |
| \(\mathrm{Attested}\langle x\rangle\) | value \(x\) carrying a valid runner signature (XR-8) |
| \(\perp\) | orthogonality (independence of two axes) |
| \(\llbracket\cdot\rrbracket\) | denotation |

---

## 2. The Central Law, Restated

The source's Final Law is correct. It is restated as the specification's root
obligation, from which the repairs descend:

\[
\boxed{
\begin{aligned}
\text{AgentProposal} &\neq \text{TrustedEvidence} \\
\text{TrustedEvidence} &= \text{AttestedObservation} \wedge \text{ValidTransition} \wedge \text{IntactLedger} \\
\text{Promotion} &= \text{TrustedEvidence} \wedge \text{ReviewAccepted} \wedge \text{Sealed} \wedge (\text{Verdict}=\mathsf{Pass})
\end{aligned}
}
\]

Everything below exists to make each conjunct **checkable by something other
than the agent's own report**. Where the source writes a conjunct it cannot
mechanically evaluate, this document either mechanizes it or downgrades its
output to `Inconclusive`. It never leaves a decorative predicate in a guard.

---

## 3. Kernel Totality and Purity

### XR-1 — Verification is total by absorption

\[
\operatorname{verifyLedger} \text{ is total; } \nexists \text{ input } \Rightarrow \bot .
\]

Every failure — malformed ledger, broken chain, unloadable policy, crashed
importer — is **denoted** as `ToolFailure` inside the assessment, never raised.

*Discharge:* **XP-1** FsAssay rule (no `failwith`/`raise`/`Option.get`/partial
match in `CanonFlow.Assurance`); FsCheck totality over arbitrary byte ledgers.

### XR-2 — No non-denoted effects in the kernel

\[
\text{Kernel} \cap (\mathit{IO}\cup\mathit{Net}\cup\mathit{Clock}\cup\mathit{Rand}\cup\mathit{Mut}) = \varnothing .
\]

The clock exclusion is load-bearing: `R-XP-021` already declares wall-clock
non-authoritative, but a kernel that reads the clock at all can leak
non-determinism into replay. The kernel receives time only as declared evidence.

*Discharge:* **XP-2** FsAssay reference rules; replay under a 10-year clock shift
yields byte-identical canonical assessment.

---

## 4. Truth and Verdict — Reused, With the Empty Case Closed

The source correctly reuses CanonFlow's two-axis truth and four-state verdict.
The reuse is kept. The **empty quantification** is not.

### XR-3 — The aggregate of no verdicts is `Inconclusive`, never `Pass` **[DIV-1]** *(critical)*

The source reducer (`R-XP-019`) returns `Pass` when its input list is empty:

```fsharp
// Source form: every List.contains is false on [], so this returns Pass.
let aggregate verdicts =
    if verdicts |> List.contains ToolFailure then ToolFailure
    elif verdicts |> List.contains Fail then Fail
    elif verdicts |> List.contains Inconclusive then Inconclusive
    else Pass                       // ← reached by aggregate []
```

`Pass` is the identity of \(\sqcup\); folding the empty list yields the identity.
A workflow that aggregated **zero gate verdicts** therefore reports success.

\[
|A| = 0 \;\Longrightarrow\; \operatorname{aggregate}(A) = \mathsf{Inconclusive}.
\]

```fsharp
let aggregate verdicts =
    match verdicts with
    | [] -> Inconclusive                     // XR-3: nothing assessed ≠ conformant
    | vs ->
        if   vs |> List.contains ToolFailure then ToolFailure
        elif vs |> List.contains Fail        then Fail
        elif vs |> List.contains Inconclusive then Inconclusive
        else Pass
```

*Discharge:* **XP-3a** `aggregate [] = Inconclusive`; **XP-3b** property
`aggregate vs ≠ Pass` whenever `vs = []`; **XP-3c** source-grep gate forbidding
the identity-fallback form.

### XR-4 — `Rejected` and `Partial` payloads are non-empty **[DIV-2]**

The source's `TransitionDecision` admits `Rejected []` — a rejection carrying no
reason — and `Partial`/`NonConformant` admit empty payloads. A rejection with no
finding is indistinguishable from an acceptance to any consumer that reads the
finding list.

\[
\mathsf{Rejected}\,[\,] \equiv \text{silent refusal};\qquad
\text{require } \mathsf{Rejected}\,(\mathrm{NE}(\mathit{Finding})).
\]

```fsharp
type TransitionDecision<'stage> =
    | Allowed  of next: 'stage * requiredEvidenceKinds: Set<string>
    | Rejected of NonEmpty<Finding>          // XR-4: a refusal must name a reason
```

*Discharge:* **XP-4** the type; no runtime test can construct the illegal state.

---

## 5. Required Gates Cannot Be Empty

### XR-5 — Non-empty gate obligation is an admission invariant **[DIV-3]** *(critical)*

The source's GREEN and promotion guards quantify universally over `RequiredGates`:

\[
\text{GREEN} \iff \forall g \in G_Q,\ \mathrm{Verdict}(g)=\mathsf{Pass}
\qquad(\text{R-XP-014})
\]

\[
\text{Promote} \Rightarrow \forall g \in \mathrm{RequiredGates}(w),\ \mathrm{Verdict}(g)=\mathsf{Pass}
\qquad(\text{R-XP-033})
\]

For \(G_Q=\varnothing\) both are **vacuously true**. A work item admitted with an
empty gate set reaches `GreenWitnessed` and satisfies the gate conjunct of
promotion having verified nothing. Since an agent may *propose* the work item
(`R-XP-007`), and nothing in the admission guard (`R-XP-010`) inspects the gate
set, an untrusted agent can propose exactly the work item that trivializes its
own promotion.

\[
\mathrm{admit}(Q) \;\Longrightarrow\; \mathrm{RequiredGates}(Q) \neq \varnothing .
\]

```fsharp
type WorkItem =
    { // ...
      RequiredGates: NonEmpty<GateId> }     // XR-5: no work item without a gate

// Admission additionally checks, per XR-5:
//   every GateId in RequiredGates resolves to a gate the runner can produce.
```

And, belt-and-suspenders, the GREEN guard is made explicit at the empty case:

\[
G_Q=\varnothing \;\Longrightarrow\; \text{GREEN} = \mathsf{Inconclusive}\ (\text{never witnessed}).
\]

*Discharge:* **XP-5a** admission-time vector: work item with empty gates → rejected
at admission, not at GREEN. **XP-5b** property: no reachable path produces
`GreenWitnessed` with an empty applicable-gate set. **XP-5c** the type.

### XR-6 — Integration re-gating is cumulative, not per-item **[DIV-4]**

The source (`R-XP-008A`) requires that integration "rerun the gates admitted for
\(Q_i\)." Integrating \(Q_i\) can regress the invariant of an earlier \(Q_{j<i}\)
whose gates are not rerun.

\[
A_{i+1} = \mathrm{Integrate}(A_i, \mathrm{VerifiedChange}(Q_i))
\;\Longrightarrow\;
\forall g \in \bigcup_{j \le i} G_{Q_j},\ \mathrm{Verdict}_{A_{i+1}}(g)=\mathsf{Pass}.
\]

A passing isolated item is not evidence that the integrated artifact passes — the
source says this — but the fix must rerun the **accumulated** obligation, not
only the newcomer's.

*Discharge:* **XP-6** falsifier: \(Q_2\) passes in isolation and its own gates
pass after integration, yet a \(Q_1\) gate now fails; assessment must be `Fail`.

---

## 6. The Agent Boundary Must Be a Signature, Not a Convention

This is the section the source most needs and least provides. The threat model
is an untrusted agent that may write any file into the workspace. The defense
must therefore be cryptographic, because file location is under the agent's
control the moment evidence is copied for verification.

### XR-7 — Runner observations are attested; agent-authored JSON cannot impersonate them **[DIV-5]** *(critical to the thesis)*

The source asserts `Authority(AgentGeneratedJson) = Untrusted` (`R-XP-022`) and
that the runner "prevents agents from rewriting authoritative history"
(`R-XP-003`), but `ExecutionObservation` (`R-XP-029`) carries **no signature and
no runner-identity binding**. Falsifier `15.3.15` ("agent-written evidence claims
to be runner evidence") has no detection mechanism at the type or crypto level.
The distinction currently rests on which directory a file sits in — a property
that does not survive the evidence being bundled and shipped to an offline
verifier.

Let \(k_R\) be a runner key the agent never holds (\(k_R \notin \mathrm{Cap}(\text{agent})\)).

\[
\mathrm{Attested}\langle o\rangle \iff \mathrm{Verify}_{\mathrm{Ed25519}}\big(\mathcal J(o),\ \sigma,\ \mathit{pub}(k_R)\big)=\mathsf{Valid}.
\]

Only an attested observation may satisfy an execution-evidence requirement:

\[
\text{RED, GREEN, Refactor gates consume } \mathrm{Attested}\langle\mathit{ExecutionObservation}\rangle \text{ only.}
\]

```fsharp
type Attested<'obs> =
    private Attested of payload: 'obs * runnerKeyId: Digest * signature: byte[]

module Attested =
    /// Sole constructor. Verifies the runner signature; returns None on any failure.
    val verify : runnerPub: PublicKey -> signed: SignedObservation -> Attested<ExecutionObservation> option
```

An agent may still *produce* an `ExecutionObservation` value — it simply cannot
produce an `Attested<ExecutionObservation>`, because the constructor is private
and gated on a key it does not have. The `15.3.15` falsifier now fails at the
type boundary.

*Discharge:* **XP-7a** the private constructor. **XP-7b** falsifier: a
well-formed but unsigned (or agent-signed) observation is rejected as
`ToolFailure`, not consumed as evidence. **XP-7c** key-disjointness
(`k_R ∉ Cap(agent)`) enforced by capability law XR-11.

### XR-8 — RED requires TestAuthor ≠ Implementer **[DIV-6]** *(high)*

The source enforces only `Reviewer ≠ Implementer` (`R-XP-016`). It names
`TestAuthor` and `Implementer` as distinct principals (`R-XP-024`) but permits
one actor to hold both. Under a single untrusted agent holding both roles, the
RED witness collapses: the agent writes the failing test *and* the implementation
that will pass it, and the only remaining guard is `FailureTargetsObligation`,
which the source does not mechanize (see XR-9).

\[
\text{WitnessedRed} \Rightarrow \mathrm{Principal}(\text{TestAuthor}) \neq \mathrm{Principal}(\text{Implementer}).
\]

Equivalently: the failing assertion and its target obligation must be pinned by
an authority that does not also author the implementation. When only one agent
is available, the pinning authority is the human/source authority who admits the
work item — which is exactly why XR-9 moves the expectation into the immutable
work item.

*Discharge:* **XP-8** falsifier: RED event where TestAuthor principal = Implementer
principal → rejected; ledger records the rejection (XR-13).

### XR-9 — `ExpectedFailure` is pinned in the immutable work item; `FailureTargetsObligation` becomes decidable **[DIV-7]** *(high)*

The source's RED guard (`R-XP-011`) contains two predicates it treats as booleans
but never grounds:

\[
\mathrm{FailureClass}_i = \mathrm{ExpectedFailure}_i
\qquad\wedge\qquad
\mathrm{FailureTargetsObligation}_i .
\]

`ExpectedFailure` has no stated provenance, and `FailureTargetsObligation` is, in
full generality, undecidable — it asks whether a test failure semantically
concerns an obligation. If the agent supplies `ExpectedFailure`, the agent
controls the equality and the guard is theatre.

Ground both by pinning, before RED, inside the digest-bound work item:

\[
Q.\mathit{obligations} \ni \big(\mathit{testId},\ \mathit{expectedAssertionDigest}\big),
\]

so the guard reduces to a decidable comparison the agent cannot rewrite without
changing \(d_Q\):

\[
\mathrm{FailureTargetsObligation}_i \iff
\mathit{observedTestId} = \mathit{testId} \;\wedge\;
H(\mathit{observedAssertion}) = \mathit{expectedAssertionDigest}.
\]

This is weaker than "the failure is semantically about the goal" — that remains
uncomputable — but it is honest: the tool proves the pinned test failed with the
pinned assertion, and the work item's author is on record for the pin.

*Discharge:* **XP-9a** RED with a failure at an unpinned test id → not witnessed.
**XP-9b** changing the pin after admission changes \(d_Q\) and voids the RED
witness (ties to `R-XP-013`).

---

## 7. Ledger Records Refusals, Not Only Victories

### XR-10 — Rejected transitions are ledger events **[DIV-8]**

The source records successful transitions and states that rejections "produce
findings but leave the trusted stage unchanged" — but does not say whether the
rejection is **appended**. If only successes enter the append-only ledger, the
ledger is a highlight reel: an agent may attempt-retry-attempt-retry until a
clean path emerges, and the artifact shows only the clean path. For an
evidence-of-process system, the struggle is the evidence.

\[
\mathrm{Reject}(s,e,\mathcal E) \;\Longrightarrow\;
\mathrm{Append}\big(\mathsf{TransitionRejected}(s,e,\mathrm{NE}(\mathit{Finding}))\big),\quad
\mathrm{stage}' = \mathrm{stage}.
\]

The stage does not advance; the attempt is nonetheless immutable and counted
against the execution budget, bounding brute-force.

```fsharp
type XpEvent =
    | Admit | WitnessRed | RecordCandidate | WitnessGreen
    | VerifyRefactor | AcceptReview | Abandon of reason: string
    | TransitionRejected of attempted: XpEvent * findings: NonEmpty<Finding>   // XR-10
```

*Discharge:* **XP-10** falsifier: three rejected `AcceptReview` attempts (stale
digest) followed by a valid one — the receipt's ledger event count includes all
four, and budget consumption is visible.

---

## 8. Ledger Integrity — Framing the Hash Inputs

### XR-11 — Capability disjointness (source law, made a set equation the key obeys)

For any AI principal \(a\):

\[
\mathrm{Cap}(a)\cap\{\mathsf{AdmitSource},\mathsf{WriteAuthoritativeLedger},\mathsf{WeakenPolicy},\mathsf{AcceptOwnReview},\mathsf{SignReceipt},\mathsf{Merge},\mathsf{Release},\ \boxed{\mathsf{HoldRunnerKey}}\ \}=\varnothing .
\]

The boxed capability is the addition: XR-7's attestation is meaningless unless
the runner signing key is provably outside every AI principal's capability set.

### XR-12 — Hash-chain inputs use unambiguous framing **[DIV-9]**

The source genesis and step digests concatenate variable-length fields:

\[
d_0 = H(\texttt{magic}\parallel \mathit{WorkflowId}\parallel \mathit{WorkItemDigest}\parallel \mathit{PolicyDigest}).
\]

Raw concatenation of variable-length values admits framing collisions
(\(H(\texttt{"ab"}\parallel\texttt{"c"}) = H(\texttt{"a"}\parallel\texttt{"bc"})\)).
Fix by canonicalizing the tuple rather than concatenating its parts:

\[
d_0 = H\big(\mathcal J(\{\texttt{magic},\ \mathit{workflow\_id},\ \mathit{work\_item\_digest},\ \mathit{policy\_digest}\})\big),
\qquad
d_i = H\big(\mathcal J(\{d_{i-1},\ \mathrm{unsigned}(e_i)\})\big).
\]

*Discharge:* **XP-12** property: no pair of distinct field tuples produces equal
genesis digests via reframing; verified by construction (JCS is injective on
distinct canonical objects).

### XR-13 — Ledger falsifiers (source list kept, extended)

Keep the source's ten (`§15.4`). Add:

\[
\text{XR-3/XR-5 vacuity},\quad
\text{unattested observation admitted as evidence},\quad
\text{rejected attempt omitted from the ledger}.
\]

---

## 9. Determinism, Monotonicity, and the Refactor Order

### XR-14 — Evidence monotonicity, with the full order stated **[DIV-10]**

The source (`R-XP-018`) defines only `Pass ≻ Inconclusive` and forbids inferring
the `Fail`/`ToolFailure` order from declaration order — but leaves the
`Fail`/`Inconclusive` relationship undefined for the monotonicity law. Removing
the evidence of a *failed* gate could then appear to launder `Fail` into
`Inconclusive`.

State the direction precisely. Let \(E' = E \setminus \{e\}\) with \(e\) required:

\[
\mathrm{verdict}(E) = \mathsf{Pass} \;\Longrightarrow\; \mathrm{verdict}(E') \neq \mathsf{Pass}
\quad(\text{Pass is upward-closed in evidence}).
\]

Removal never *improves* toward promotability. Whether removal maps `Fail` to
`Inconclusive` is moot in practice: the ledger is append-only and evidence is
content-addressed, so **evidence cannot be removed without breaking the chain**.
The monotonicity law is therefore enforced by immutability (XR-12), not by
recomputation over a mutable evidence set. State this; do not leave the reader to
assume a mutable-set semantics the ledger forbids.

*Discharge:* **XP-14** FsCheck over the immutable evidence graph: no chain-valid
edit reaches a strictly-more-promotable verdict.

### XR-15 — `ObservableBehaviour` equality is gate-observable, not semantic **[DIV-11]**

The source refactor law (`R-XP-015`) asserts
\(\mathrm{ObservableBehaviour}(c_r)=\mathrm{ObservableBehaviour}(c_g)\) as if
decidable. True behavioral equivalence is undecidable. Qualify it to what the
tool can witness:

\[
\mathrm{ObservableBehaviour}_{\mathcal G}(c_r)=\mathrm{ObservableBehaviour}_{\mathcal G}(c_g)
\iff
\forall g\in \mathcal G,\ \mathrm{result}(g,c_r)=\mathrm{result}(g,c_g),
\]

for the admitted gate suite \(\mathcal G\). This is a **lower bound** on
equivalence and the receipt must say so — a refactor that passes is preserving
*observed* behavior, not proven-total behavior. Claiming more is a claim-gate
violation of the kind the ONDCFlow spec was careful to forbid.

### XR-16 — Refactor quality is a product order; tradeoffs need a decision record **[DIV-12]**

The source uses a partial order \(\succeq\) over quality (`R-XP-015`). A partial
order leaves legitimate tradeoffs **incomparable**: a refactor that improves
typing while increasing line count may satisfy neither \(c_r\succeq c_g\) nor
\(c_g\succeq c_r\), and the guard silently rejects an improvement.

Use a **product order** over declared dimensions \(q_1,\dots,q_m\):

\[
c_r \succeq c_g \iff \forall k,\ q_k(c_r)\ \ge\ q_k(c_g).
\]

An improvement on some dimensions and a regression on others is *not* a
monotone refactor; it is a **design change** and requires an explicit
`QualityTradeoff` decision record admitted by the policy owner. Incomparability
becomes an explicit human decision, not a silent rejection.

*Discharge:* **XP-16** vector: a typing-improving, size-increasing refactor is
accepted only with the decision record; without it, `Rejected (NE finding)`.

---

## 10. Seal, Promotion, and the One Idea to Promote

### XR-17 — Seal ⊥ Verdict **(promoted from R-XP-006, kept intact)**

\[
\mathrm{SealState} \perp \mathrm{Verdict},\qquad
\mathrm{Promotable}(w) \iff \mathrm{SealState}(w)=\mathsf{Sealed}\ \wedge\ \mathrm{Verdict}(w)=\mathsf{Pass}\ \wedge\ \mathrm{Stage}(w)=\mathsf{ReviewAccepted}.
\]

This is the document's best idea and needs no repair: a failed, inconclusive, or
abandoned run is *sealed* so its evidence survives, while remaining
non-promotable. The only clarification: a "receipt" with `seal.status = Unsealed`
is a **draft assessment**, and the term *receipt* in claim language must be
reserved for the sealed form, so that "here is the receipt" never denotes an
unsealed draft.

### XR-18 — Protected policy digest **(kept from R-XP-033)**

\[
\mathrm{Promote}(w) \Rightarrow \mathrm{PolicyDigest}(w)=\mathrm{ProtectedPolicyDigest}.
\]

An agent cannot substitute a weaker policy: the promoted policy digest must equal
the digest the protected authority pinned. Combined with XR-11
(`WeakenPolicy ∉ Cap(agent)`) and `R-XP-026` (policy changes are separate work),
this closes the policy-substitution path.

---

## 11. Observable Contract

### XR-19 — Exit semantics for every command **[DIV-13]**

The source defines exit codes implicitly via the verdict but does not tabulate
the CLI's two commands. Mirror the ONDCFlow discipline:

| Command | 0 | 1 | 2 | 3 | 64 |
|---|---|---|---|---|---|
| `assurance verify` | Pass | Fail | Inconclusive | ToolFailure | bad invocation |
| `assurance receipt` | sealed & written | refused (non-promotable asked to promote) | — | tool failure | bad invocation |

\[
\mathrm{exit}^{-1}(0)=\{\mathsf{Pass}\};\qquad \text{no } \mathsf{Inconclusive} \text{ path returns } 0.
\]

### XR-20 — Secret and silence boundary **(kept from R-XP-036, extended)**

\[
\{\text{private receipt key},\ \text{runner private key},\ \text{raw secret}\}\notin \text{stdout}\cup\text{stderr}\cup\text{logs}\cup\text{ledger}\cup\text{receipt}.
\]

*Discharge:* **XP-20** canary key + canary secret grepped across every emitted
artifact and stream; any hit fails the build.

---

## 12. Release Gate Reachability

### XR-21 — `M5` pilot gate is the true terminus; state it now

The source's Definition of Done ends with "a real repository pilot demonstrates
fail-closed protected promotion" (`M5`). Every gate before `M5` is discharged by
compiler or property tests over small carriers; `M5` is the only gate requiring
an *external* runner and a *real* repository. As with ONDCFlow's `G6`, name the
consequence in advance:

\[
\nexists\ \text{attesting runner} \;\Longrightarrow\; M5 \neq \mathsf{Pass} \;\Longrightarrow\; \text{no fail-closed promotion is demonstrated}.
\]

The Crucible Runner is not a detail of `M5`; it is the precondition for the
document's entire claim. Until an attesting runner exists (XR-7), the enhancement
is a **verifier of ledgers the runner has not yet produced** — sound, complete,
and untested against reality. Plan for that state explicitly; do not discover it
at the pilot.

---

## 13. Build Order, Gated

Sequence unchanged from the source. Requirement gates attached as exit criteria.

| Milestone | Adds | Exit requires |
|---|---|---|
| **M0** Ownership lock | ADR, dependency-direction test, reuse of Core types | XR-11, dependency law `Core ← Assurance ← Xp` |
| **M1** Generic kernel | identity, snapshot, evidence refs, ledger, replay | XR-1, XR-2, XR-3, XR-4, XR-12, XR-14 |
| **M2** Minimal XP profile | stages, events, admission/RED/candidate/GREEN, WIP=1 | XR-5, XR-8, XR-9, XR-10 |
| **M3** Refactor & review | preservation, no-op, reviewer independence, abandonment | XR-15, XR-16, seal/verdict cases |
| **M4** Assessment & receipt | two-axis, reducer, canonical receipt, seal separation | XR-17, XR-18, XR-19, XR-3 again |
| **M5** Runner & FsAssay | attesting runner, qualified import, protected CI, pilot | XR-7, XR-20, XR-21 |

**M1 is buildable today.** It contains no XP vocabulary and no agent: pure
ledger algebra over a four-element verdict set. XR-3, XR-4, XR-12 and XR-14 are
all discharged by the compiler or by exhaustive property tests. If a fun place to
start is wanted, it is here — with the caveat in §17.

---

## 14. Cherry-Pick Ledger

| # | Source element | Verdict | Note |
|---|---|---|---|
| 1 | `R-XP-006` seal ⊥ verdict | **Promote** | best idea in the document → XR-17 |
| 2 | `R-XP-011` RED decomposition | **Keep + strengthen** | ground the two ungrounded predicates → XR-9 |
| 3 | `R-XP-013` test-integrity hash | **Keep** | `H(T_red)=H(T_green)` is right; scope defined in D13 |
| 4 | `R-XP-014` environment-digest equality | **Keep** | catches toolchain drift between RED and GREEN |
| 5 | `R-XP-020/021` append-only, hash-order-authoritative | **Keep + frame** | fix concatenation → XR-12 |
| 6 | `R-XP-022` agent evidence untrusted | **Promote** | prose → signed attestation → XR-7 |
| 7 | `R-XP-008A` integration ≠ summation | **Repair** | make re-gating cumulative → XR-6 |
| 8 | `R-XP-025` no self-promotion | **Keep + extend** | add `HoldRunnerKey` to the excluded set → XR-11 |
| 9 | `R-XP-033` protected policy digest | **Keep** | closes policy substitution → XR-18 |
| 10 | `R-XP-026` policy change is separate work | **Keep** | prevents retroactive legalization |
| 11 | `R-XP-027` WIP=1 | **Keep** | bounds concurrency; complements XR-10 budget bound |
| 12 | Two-axis truth / four-state verdict reuse | **Keep** | no second verdict algebra |
| 13 | `R-XP-019` explicit reducer | **Repair** | empty case returns `Pass` → XR-3 |
| 14 | `TransitionDecision.Rejected of Finding list` | **Reject** | empty rejection representable → XR-4 |
| 15 | `RequiredGates: GateId list` | **Reject** | empty gate set → vacuous GREEN/promotion → XR-5 |
| 16 | `R-XP-015` refactor `ObservableBehaviour =` | **Repair** | undecidable as stated; qualify to gate-observable → XR-15 |
| 17 | `R-XP-015` quality partial order `⪰` | **Repair** | incomparability silently rejects → product order → XR-16 |
| 18 | `R-XP-018` monotonicity | **Repair** | order under-defined; state immutability enforcement → XR-14 |
| 19 | Dependency law `Core ← Assurance ← Xp` | **Keep verbatim** | clean and correct |
| 20 | `§15` falsifier corpus | **Keep + extend** | add vacuity and unattested-observation falsifiers → XR-13 |
| 21 | `§19` deferred list | **Keep deferred** | no exceptions |
| 22 | `R-XP-024` ten principals | **Keep + require** | mandate TestAuthor ≠ Implementer and Runner ≠ Implementer → XR-8, XR-11 |

---

## 15. Defect Register Against the Source

| ID | Severity | Location | Defect | Requirement |
|---|---|---|---|---|
| **D1** | **Critical** | `R-XP-019` reducer | `aggregate []` returns `Pass`. A workflow that aggregated zero gate verdicts reports success — the same vacuous-`Pass` defect this author's ONDCFlow reducer had. | XR-3 |
| **D2** | **Critical** | `R-XP-014`, `R-XP-033` | `∀ g ∈ RequiredGates` is vacuously true when the set is empty. An agent may propose a work item with no gates and trivially satisfy GREEN and the gate conjunct of promotion. Admission (`R-XP-010`) does not inspect the gate set. | XR-5 |
| **D3** | **High** | `R-XP-022`, `R-XP-029` | The untrusted-agent boundary is asserted but not mechanized. `ExecutionObservation` has no signature or runner-key binding; falsifier `15.3.15` has no type-level or cryptographic detector. The distinction rests on filesystem location. | XR-7 |
| **D4** | **High** | `R-XP-024`, `R-XP-016` | Only `Reviewer ≠ Implementer` is required. `TestAuthor = Implementer` and `Runner = Implementer` are permitted, collapsing the RED witness and the observation under a single agent. | XR-8, XR-11 |
| **D5** | **High** | `R-XP-011` | `FailureClass = ExpectedFailure` and `FailureTargetsObligation` are written as decidable guards with no provenance for `ExpectedFailure` and no discharge for the obligation-targeting predicate (undecidable in general). | XR-9 |
| **D6** | Medium | `TransitionDecision` | `Rejected of Finding list` admits `Rejected []` — a rejection with no reason, indistinguishable from acceptance to a consumer reading findings. | XR-4 |
| **D7** | Medium | `§5`, `R-XP-013` | Whether rejected transition attempts enter the append-only ledger is undefined. If only successes are recorded, the ledger is a highlight reel and permits unbounded silent retry. | XR-10 |
| **D8** | Medium | `R-XP-008A` | Integration reruns only "the gates admitted for \(Q_i\)"; a regression \(Q_i\) causes in an earlier item's gates goes undetected. | XR-6 |
| **D9** | Medium | `R-XP-015` | `ObservableBehaviour(c_r) = ObservableBehaviour(c_g)` is stated as if decidable; it is decidable only relative to the admitted gate suite and overclaims semantic equivalence. | XR-15 |
| **D10** | Medium | `R-XP-015` | Quality `⪰` is a partial order; incomparable quality vectors (legitimate tradeoffs) satisfy neither direction and are silently rejected. | XR-16 |
| **D11** | Medium | `R-XP-018` | Monotonicity defines only `Pass ≻ Inconclusive`; the `Fail`/`Inconclusive` relationship is undefined, and the law implicitly assumes a mutable evidence set the append-only ledger forbids. | XR-14 |
| **D12** | Low | `R-XP-020` | Hash-chain inputs concatenate variable-length fields (`magic ‖ id ‖ …`) without unambiguous framing, a theoretical reframing-collision surface. | XR-12 |
| **D13** | Low | `R-XP-013` | Scope of the hashed test set `T_red`/`T_green` is undefined — whole-suite (over-rigid) versus per-test (needs a mapping) changes the law's meaning. | XR-9 discharge note |
| **D14** | Low | `§15` | The falsifier corpus omits the vacuity cases (empty gate set → GREEN; empty aggregate → Pass), consistent with the spec not recognizing D1/D2. | XR-13 |

**Assessment of the source.** Fourteen defects, two critical — the same
head-count as the ONDCFlow review, and the same *signature* critical
(vacuous `Pass` by empty universal quantification) reappearing in two fresh
sites. That recurrence is itself the most useful datum: it is not a typo, it is
a **generation-time blind spot**. Whatever produced these specs does not
instinctively close the empty case, so every future spec from the same source
should be grepped for `∀ … ∈ (possibly-empty set)` and for reducers whose
`else` branch is the lattice identity, *first*, before anything else is read.

The new defect class — D3/D4/D5 — is not a blind spot but a genuine hard
problem: mechanizing "the agent cannot lie about what it ran." The source
gestured at it correctly and then wrote the guard as though the gesture were the
mechanism. The repair (a private `Attested<>` constructor gated on a key outside
the agent's capability set) is the honest minimum, and it is the one piece of
this enhancement that cannot be built without the external Crucible Runner
actually existing.

---

## 16. Proof Obligation Register

| Req | Discharge | Instrument | Milestone |
|---|---|---|---|
| XR-1 | XP-1 | FsAssay + FsCheck totality | M1 |
| XR-2 | XP-2 | FsAssay + clock-shift replay | M1 |
| XR-3 | XP-3a/b/c | vector + property + grep | M1/M4 |
| XR-4 | XP-4 | the type | M1 |
| XR-5 | XP-5a/b/c | admission vector + reachability property + type | M2 |
| XR-6 | XP-6 | cross-item regression falsifier | M2 |
| XR-7 | XP-7a/b/c | private constructor + unsigned-rejection + key disjointness | M5 |
| XR-8 | XP-8 | principal-equality falsifier | M2 |
| XR-9 | XP-9a/b | pinned-expectation vectors | M2 |
| XR-10 | XP-10 | retry-visibility falsifier | M2 |
| XR-11 | — | capability set equation + policy test | M0 |
| XR-12 | XP-12 | JCS-injectivity argument | M1 |
| XR-14 | XP-14 | immutable-graph property | M1 |
| XR-15 | — | receipt claim wording + gate-relative equality | M3 |
| XR-16 | XP-16 | tradeoff decision-record vector | M3 |
| XR-17 | — | seal/verdict orthogonality vectors | M4 |
| XR-18 | — | protected-digest mismatch vector | M4 |
| XR-19 | — | per-command exit vectors | M4 |
| XR-20 | XP-20 | canary grep over all artifacts | M5 |
| XR-21 | — | stated reachability lemma | M5 |

A requirement with no row is not a requirement. A row whose only instrument is
"review" is not discharged.

---

## 17. Closing Position

\[
\boxed{
\text{Attested Observation} \to \text{Valid Transition} \to \text{Intact Ledger} \to \text{Honest Verdict} \to \text{Sealed, Non-Laundered Receipt}
}
\]

The source had the shape right. Three sentences carry the repair, and each is now
a requirement rather than a paragraph:

\[
\mathrm{aggregate}(\varnothing)=\mathsf{Inconclusive}
\qquad(\text{XR-3, and } \mathrm{RequiredGates}\neq\varnothing \text{ by XR-5})
\]

\[
\mathrm{Attested}\langle o\rangle \iff \mathrm{Verify}_{k_R}(o),\quad k_R\notin\mathrm{Cap}(\text{agent})
\qquad(\text{XR-7})
\]

\[
\mathrm{TestAuthor}\neq\mathrm{Implementer} \;\wedge\; \big(\mathit{testId},\ \mathit{expectedAssertion}\big)\in Q
\qquad(\text{XR-8, XR-9})
\]

The first closes the empty proof. The second makes the agent's report
uncounterfeitable. The third makes the RED witness mean what TDD needs it to
mean when the author of the test is also the author of the code — which, with a
single agent, it always is.

Everything else in this document is bookkeeping around those three.

\[
\blacksquare
\]
