namespace CanonFlow.Evaluator

open System
open System.IO
open CanonFlow.Assurance
open ONDCFlow.Core
open ONDCFlow.Profile.Retail
open FsToolkit.ErrorHandling

module OndcRunner =
    let private rulePack : OndcRulePack = {
        Id = "ondc-retail-order-formation-preview-v2"
        Digest = AdmittedSource.retailRulePackDigestText
        ApplicableRules = 10
        Evaluate = fun input ->
            Rules.evaluateEvidence input
            @ [ RetailRules.rule_retail_valid_guids input.Traces ]
    }

    let run (manifest: EvaluationManifest) (budget: EvaluationBudget) =
        result {
            let! evidencePath =
                match manifest.Subject.Artifacts with
                | first :: _ ->
                    let path =
                        if Path.IsPathRooted(first) then first
                        else Path.Combine(manifest.Subject.Root, first)
                    Ok path
                | [] -> Error "ONDC profile requires an evidence-bundle artifact."
            let! bundle =
                try Ok (File.ReadAllText(evidencePath))
                with ex -> Error $"Cannot read ONDC evidence bundle: {ex.Message}"
            let! _ =
                try
                    use document =
                        System.Text.Json.JsonDocument.Parse(
                            bundle,
                            System.Text.Json.JsonDocumentOptions(MaxDepth = budget.MaxJsonDepth))
                    Ok ()
                with ex ->
                    Error $"ONDC evidence bundle exceeds its JSON budget or is malformed: {ex.Message}"
            let! sourceLock = AdmittedSource.getVerifiedRetail120 ()
            let receipt =
                Assessor.evaluateBundle
                    rulePack
                    bundle
                    sourceLock
                    manifest.EvaluationContext.Instant
                    ("sha256:" + Hash.computeSha256 bundle)
            return receipt.Assessments |> List.head
        }
