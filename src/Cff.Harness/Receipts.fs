namespace Cff.Harness

open System
open System.Globalization
open System.Text
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Signers
open CanonFlow.Assurance.Contracts

[<RequireQualifiedAccess>]
module Receipts =
    let private utc (value: DateTimeOffset) =
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)

    let private sign (seed: byte array) (bytes: byte array) =
        let key = Ed25519PrivateKeyParameters(seed, 0)
        let signer = Ed25519Signer()
        signer.Init(true, key)
        signer.BlockUpdate(bytes, 0, bytes.Length)
        signer.GenerateSignature()

    let publicKey (seed: byte array) =
        Ed25519PrivateKeyParameters(seed, 0).GeneratePublicKey().GetEncoded()

    let private verify (publicKey: byte array) (bytes: byte array) (signature: byte array) =
        if isNull publicKey || publicKey.Length <> 32 || isNull signature || signature.Length <> 64 then false
        else
            let verifier = Ed25519Signer()
            verifier.Init(false, Ed25519PublicKeyParameters(publicKey, 0))
            verifier.BlockUpdate(bytes, 0, bytes.Length)
            verifier.VerifySignature(signature)

    let private admissionPayload (receipt: RulePackAdmissionReceipt) =
        String.concat "\n" [
            "cff-admission/v1"
            ContentDigest.value receipt.RulePackDigest
            ContentDigest.value receipt.SourceProfileDigest
            ContentDigest.value receipt.PolicyDigest
            string receipt.Decision
            (receipt.FindingsDigest |> Option.map ContentDigest.value |> Option.defaultValue "")
            receipt.AdmittedBy
            utc receipt.AdmittedAt
            (receipt.PreviousAdmissionDigest |> Option.map ContentDigest.value |> Option.defaultValue "")
            receipt.SignatureAlgorithm
        ]
        |> Encoding.UTF8.GetBytes

    let admitPack
        (seed: byte array)
        (principal: string)
        (admittedAt: DateTimeOffset)
        (pack: RulePackDefinition)
        =
        let draft: RulePackAdmissionReceipt =
            { RulePackDigest = Canonical.rulePackDigest pack
              SourceProfileDigest = pack.SourceProfileDigest
              PolicyDigest = pack.AggregationPolicyDigest
              Decision = Admitted
              FindingsDigest = None
              AdmittedBy = principal
              AdmittedAt = admittedAt
              PreviousAdmissionDigest = None
              SignatureAlgorithm = "Ed25519"
              Signature = Array.empty }
        { draft with Signature = sign seed (admissionPayload draft) }

    let verifyAdmission
        (publicKey: byte array)
        (pack: RulePackDefinition)
        (receipt: RulePackAdmissionReceipt)
        =
        receipt.Decision = Admitted
        && receipt.RulePackDigest = Canonical.rulePackDigest pack
        && receipt.SourceProfileDigest = pack.SourceProfileDigest
        && receipt.PolicyDigest = pack.AggregationPolicyDigest
        && receipt.SignatureAlgorithm = "Ed25519"
        && verify publicKey (admissionPayload receipt) receipt.Signature

    let admissionDigest (receipt: RulePackAdmissionReceipt) =
        admissionPayload receipt
        |> Array.append receipt.Signature
        |> Canonical.sha256Bytes

    let private assessmentText = function
        | NotApplicableAssessment reason -> "not-applicable:" + reason
        | CompletedAssessment Satisfied -> "satisfied"
        | CompletedAssessment (ToolFailure error) -> "tool-failure:" + error
        | CompletedAssessment (Inconclusive missing) ->
            missing
            |> NonEmptyList.toList
            |> List.map (fun item -> item.RequirementId + ":" + item.Reason)
            |> List.sort
            |> String.concat "|"
            |> (+) "inconclusive:"
        | CompletedAssessment (Violated findings) ->
            findings
            |> NonEmptyList.toList
            |> List.map (fun finding ->
                String.concat ":" [
                    RuleId.value finding.RuleId
                    finding.Code
                    string finding.Severity
                    finding.Message
                    finding.Expected |> Option.defaultValue ""
                    finding.Observed |> Option.defaultValue ""
                    ClauseId.value finding.Authority
                ])
            |> List.sort
            |> String.concat "|"
            |> (+) "violated:"

    let ruleResultsDigest (results: RuleResult list) =
        results
        |> List.sortBy (fun result -> RuleId.value result.RuleId)
        |> List.map (fun result ->
            String.concat "\n" [
                RuleId.value result.RuleId
                ContentDigest.value result.DefinitionDigest
                ContentDigest.value result.EvaluatorIdentity.AssemblyDigest
                ContentDigest.value result.EvaluatorIdentity.DependencySetDigest
                assessmentText result.Assessment
            ])
        |> String.concat "\n--\n"
        |> Canonical.sha256Text

    let evaluatorSetDigest (results: RuleResult list) =
        results
        |> List.map _.EvaluatorIdentity
        |> List.distinct
        |> List.sortBy (fun identity -> EvaluatorId.value identity.EvaluatorId)
        |> List.map (fun identity ->
            String.concat ":" [
                EvaluatorId.value identity.EvaluatorId
                ContentDigest.value identity.AssemblyDigest
                ContentDigest.value identity.DependencySetDigest
                identity.PackageVersion
                identity.RuntimeVersion
                identity.RuntimeIdentifier
                ContentDigest.value identity.BuildProvenanceDigest
            ])
        |> String.concat "\n"
        |> Canonical.sha256Text

    let private verdictText = function
        | Pass -> "Pass"
        | ProfileToolFailure error -> "ProfileToolFailure:" + error
        | InconclusiveProfile missing ->
            missing
            |> NonEmptyList.toList
            |> List.map (fun item -> item.RequirementId + ":" + item.Reason)
            |> List.sort
            |> String.concat "|"
            |> (+) "InconclusiveProfile:"
        | Fail findings ->
            findings
            |> NonEmptyList.toList
            |> List.map (fun finding -> RuleId.value finding.RuleId + ":" + finding.Code)
            |> List.sort
            |> String.concat "|"
            |> (+) "Fail:"

    let private evaluationPayload (receipt: RulePackEvaluationReceipt) =
        String.concat "\n" [
            "cff-evaluation/v1"
            string receipt.ReceiptVersion
            ContentDigest.value receipt.RulePackDigest
            ContentDigest.value receipt.AdmissionReceiptDigest
            ContentDigest.value receipt.SourceProfileDigest
            ContentDigest.value receipt.AggregationPolicyDigest
            ContentDigest.value receipt.CanonicalizationProfileDigest
            ContentDigest.value receipt.EvaluatorSetDigest
            ContentDigest.value receipt.ToolchainDigest
            ContentDigest.value receipt.SandboxPolicyDigest
            ContentDigest.value receipt.EvidenceBundleDigest
            ContentDigest.value receipt.ApplicabilityFactsDigest
            ContentDigest.value receipt.RuleResultsDigest
            verdictText receipt.ProfileVerdict
            utc receipt.EvaluatedAt
            (receipt.PreviousReceiptDigest |> Option.map ContentDigest.value |> Option.defaultValue "")
            receipt.Issuer
            receipt.SignatureAlgorithm
        ]
        |> Encoding.UTF8.GetBytes

    let sealEvaluation
        (seed: byte array)
        (issuer: string)
        (evaluatedAt: DateTimeOffset)
        (toolchainDigest: ContentDigest)
        (sandboxPolicyDigest: ContentDigest)
        (evaluation: EvaluatedRulePack)
        =
        let draft: RulePackEvaluationReceipt =
            { ReceiptVersion = 1
              RulePackDigest = evaluation.PackDigest
              AdmissionReceiptDigest = admissionDigest evaluation.Admission
              SourceProfileDigest = evaluation.Pack.SourceProfileDigest
              AggregationPolicyDigest = evaluation.Pack.AggregationPolicyDigest
              CanonicalizationProfileDigest = evaluation.Pack.CanonicalizationProfileDigest
              EvaluatorSetDigest = evaluatorSetDigest evaluation.Results
              ToolchainDigest = toolchainDigest
              SandboxPolicyDigest = sandboxPolicyDigest
              EvidenceBundleDigest = evaluation.Evidence.BundleDigest
              ApplicabilityFactsDigest = evaluation.Facts.FactsDigest
              RuleResultsDigest = ruleResultsDigest evaluation.Results
              ProfileVerdict = evaluation.Verdict
              EvaluatedAt = evaluatedAt
              PreviousReceiptDigest = None
              Issuer = issuer
              SignatureAlgorithm = "Ed25519"
              Signature = Array.empty }
        { draft with Signature = sign seed (evaluationPayload draft) }

    let verifyEvaluation
        (publicKey: byte array)
        (admissionPublicKey: byte array)
        (evaluation: EvaluatedRulePack)
        (receipt: RulePackEvaluationReceipt)
        =
        let recomputed = Engine.aggregate evaluation.Results
        let checks = [
            "admission signature", verifyAdmission admissionPublicKey evaluation.Pack evaluation.Admission
            "rule-pack digest", receipt.RulePackDigest = Canonical.rulePackDigest evaluation.Pack
            "admission digest", receipt.AdmissionReceiptDigest = admissionDigest evaluation.Admission
            "source profile", receipt.SourceProfileDigest = evaluation.Pack.SourceProfileDigest
            "aggregation policy", receipt.AggregationPolicyDigest = evaluation.Pack.AggregationPolicyDigest
            "canonicalization profile", receipt.CanonicalizationProfileDigest = evaluation.Pack.CanonicalizationProfileDigest
            "evaluator set", receipt.EvaluatorSetDigest = evaluatorSetDigest evaluation.Results
            "evidence item integrity", Evidence.verifyIntegrity evaluation.Evidence |> List.isEmpty
            "evidence bundle", receipt.EvidenceBundleDigest = Canonical.evidenceBundleDigest evaluation.Evidence
            "applicability facts", receipt.ApplicabilityFactsDigest = Canonical.factsDigest evaluation.Facts.Facts
            "rule results", receipt.RuleResultsDigest = ruleResultsDigest evaluation.Results
            "profile verdict", receipt.ProfileVerdict = recomputed
        ]
        let verdictStatus =
            checks
            |> List.tryFind (snd >> not)
            |> function
                | Some (name, _) -> Invalid ("Protected " + name + " does not verify")
                | None -> Valid
        let sealStatus =
            if receipt.SignatureAlgorithm <> "Ed25519" then Invalid "Unsupported signature algorithm"
            elif verify publicKey (evaluationPayload receipt) receipt.Signature then Valid
            else Invalid "Evaluation signature does not verify"
        { SealStatus = sealStatus
          VerdictStatus = verdictStatus
          RecomputedVerdict = recomputed }
