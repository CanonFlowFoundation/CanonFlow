# Phase M12: External Certification Workflow

**Status:** Defined and operational  
**Authority:** Third-Party / External Governance only  
**Subject:** CanonFlow and ONDCFlow  

## 1. Governance Law: The Claims Boundary

As stipulated in the `CANONFLOW_EVALUATOR_DOTNET10_IMPLEMENTATION.md` MVP specification, CanonFlow Foundation and its tools **MUST NOT** self-award compliance certificates. 

The following claims are strictly **FORBIDDEN** without written authority:
- ONDC Certified
- ONDC Approved
- Official ONDC Compliance Certificate
- CanonFlow Foundation guarantees compliance

The maximum permitted internal claim is:
> "CanonFlow Conformance Assessment — Assessed against profile <id>@<digest>"

## 2. External Certification Framework

To support official external certification, this framework establishes a verifiable process whereby an authorized governance body can utilize CanonFlow Evaluator to produce a cryptographically sealed canonical receipt (`assessment.cff`).

### 2.1 The Source Lock Gate

No ONDC protocol rule implementation may be certified until an authorized reviewer admits a `source.lock.json`. This lock file must contain:

- Profile identity
- Admitted source documents (e.g., Swagger, JSON schemas)
- Exact versions and effective dates
- Reviewer identity
- Source-lock digest/signature

If the source lock is absent or invalid, the overall Verdict MUST be `Inconclusive`.

### 2.2 The Certification Automation Workflow

To prevent unauthorized manual generation of certificates, the certification procedure is codified in a gated GitHub Action (`.github/workflows/certify.yml`).

#### Workflow Requirements:
1. **Manual Trigger Only**: The workflow can only be invoked via `workflow_dispatch`.
2. **Key Injection**: The workflow requires the Authorized Governance Body to supply their Private Key securely (via GitHub Secrets or an HSM integration).
3. **Receipt Construction**: The workflow executes `canonflow evaluate` using the pinned evaluator image and produces a sealed canonical receipt.
4. **Publishing**: The final `.cff` receipt is published to an auditable transparency log or artifact repository.

## 3. Offline Verification

Once a certified receipt is issued, it can be verified fully offline by any participant using the `canonflow receipt verify` command:

```bash
canonflow receipt verify \
  --receipt /input/assessment.cff \
  --public-key /input/evaluator.pub \
  --offline
```

This verifies the canonical digest and the Ed25519 seal without relying on network endpoints.
