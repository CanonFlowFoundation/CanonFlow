namespace Cff.Harness

open System
open CanonFlow.Assurance.Contracts

[<RequireQualifiedAccess>]
module Validation =
    let private blank name value =
        if String.IsNullOrWhiteSpace value then [ name + " cannot be blank" ] else []

    let rec private applicabilityErrors path = function
        | AllOf [] -> [ path + ": AllOf cannot be empty" ]
        | AnyOf [] -> [ path + ": AnyOf cannot be empty" ]
        | AllOf values | AnyOf values ->
            values
            |> List.mapi (fun index value -> applicabilityErrors ($"{path}[{index}]") value)
            |> List.concat
        | Not value -> applicabilityErrors (path + ".not") value
        | _ -> []

    let definition (definition: ObligationDefinition) =
        let requirementErrors =
            let identifiers = definition.RequiredEvidence |> List.map _.RequirementId
            let duplicates =
                identifiers
                |> List.countBy id
                |> List.choose (fun (identifier, count) ->
                    if count > 1 then Some ("Duplicate requirement ID: " + identifier) else None)
            let blanks =
                definition.RequiredEvidence
                |> List.collect (fun requirement ->
                    blank "Requirement ID" requirement.RequirementId
                    @ blank "Requirement description" requirement.Description)
            let zeroOnly =
                match definition.RequiredEvidence with
                | [] -> [ "An evidence-backed obligation requires evidence requirements" ]
                | values when values |> List.forall (fun value -> value.Cardinality = ZeroOrMore) ->
                    [ "ZeroOrMore cannot be the only basis for Satisfied" ]
                | _ -> []
            blanks @ duplicates @ zeroOnly
        blank "Title" definition.Title
        @ (if definition.RuleVersion <= 0 then [ "RuleVersion must be positive" ] else [])
        @ blank "Admission principal" definition.Authority.AdmittedBy
        @ blank "Interpretation note" definition.Authority.InterpretationNote
        @ applicabilityErrors "$.applicability" definition.Applicability
        @ requirementErrors

    let pack registry (pack: RulePackDefinition) =
        let duplicateRules =
            pack.Obligations
            |> List.countBy (fun obligation -> obligation.RuleId, obligation.RuleVersion)
            |> List.choose (fun ((ruleId, version), count) ->
                if count > 1 then
                    Some ($"Duplicate rule identity: {RuleId.value ruleId}/v{version}")
                else None)
        let missingEvaluators =
            pack.Obligations
            |> List.choose (fun obligation ->
                if Map.containsKey obligation.EvaluatorId registry then None
                else Some ("Evaluator unavailable: " + EvaluatorId.value obligation.EvaluatorId))
        (if pack.Version <= 0 then [ "Rule-pack version must be positive" ] else [])
        @ (if List.isEmpty pack.Obligations then [ "Rule pack cannot be empty" ] else [])
        @ duplicateRules
        @ missingEvaluators
        @ (pack.Obligations |> List.collect definition)

[<RequireQualifiedAccess>]
module Applicability =
    let private conventionPath prefix value =
        FactPath.create (prefix + value) |> Result.defaultWith invalidOp

    let private find path (context: ApplicabilityContext) =
        context.Facts |> List.tryFind (fun fact -> fact.Path = path)

    let rec evaluate expression context =
        let factEquals path expected =
            match find path context with
            | None -> ApplicabilityUndetermined [ path ]
            | Some fact when fact.Value = expected -> Applicable
            | Some _ -> NotApplicable ("Fact does not match: " + FactPath.value path)

        match expression with
        | Always -> Applicable
        | ProfileIs expected ->
            if context.Profile = expected then Applicable else NotApplicable "Profile does not match"
        | DomainIs value -> factEquals (conventionPath "$.domain:" "") (FactValue.Text value)
        | VersionIs value -> factEquals (conventionPath "$.version:" "") (FactValue.Text value)
        | RoleIs value -> factEquals (conventionPath "$.role:" "") (FactValue.Text value)
        | FlowIs value -> factEquals (conventionPath "$.flow:" "") (FactValue.Text value)
        | ActionPresent value -> factEquals (conventionPath "$.action:" value) (FactValue.Boolean true)
        | FactEquals (path, expected) -> factEquals path expected
        | Not value ->
            match evaluate value context with
            | Applicable -> NotApplicable "Negated applicability expression matched"
            | NotApplicable _ -> Applicable
            | ApplicabilityUndetermined missing -> ApplicabilityUndetermined missing
        | AllOf values ->
            let results = values |> List.map (fun value -> evaluate value context)
            match results |> List.tryPick (function NotApplicable reason -> Some reason | _ -> None) with
            | Some reason -> NotApplicable reason
            | None ->
                let missing =
                    results
                    |> List.collect (function ApplicabilityUndetermined paths -> paths | _ -> [])
                    |> List.distinct
                if List.isEmpty missing then Applicable else ApplicabilityUndetermined missing
        | AnyOf values ->
            let results = values |> List.map (fun value -> evaluate value context)
            if results |> List.exists ((=) Applicable) then Applicable
            else
                let missing =
                    results
                    |> List.collect (function ApplicabilityUndetermined paths -> paths | _ -> [])
                    |> List.distinct
                if List.isEmpty missing then NotApplicable "No applicability alternative matched"
                else ApplicabilityUndetermined missing

[<RequireQualifiedAccess>]
module Evidence =
    let private trustRank = function
        | ProducerSupplied -> 0
        | IndependentlyCaptured -> 1
        | AuthenticatedExternalSource -> 2

    let private kindMatches requirement item =
        match requirement, item with
        | ProtocolMessage expected, Message message ->
            String.Equals(expected, message.Action, StringComparison.Ordinal)
        | PairedMessage (left, right), Message message ->
            message.Action = left || message.Action = right
        | RegistryObservation, Registry _ -> true
        | expected, Observation observation -> expected = observation.Kind
        | _ -> false

    let private itemTrust = function
        | Message message -> message.Provenance.EstablishedTrust
        | Registry registry -> registry.Provenance.EstablishedTrust
        | Observation observation -> observation.Provenance.EstablishedTrust

    let verifyIntegrity (bundle: EvidenceBundle) =
        let itemErrors =
            bundle.Items
            |> List.collect (function
                | Message message ->
                    let raw =
                        if Canonical.verifyDigest message.RawPayloadDigest message.RawPayload then []
                        else [ $"Raw payload digest mismatch: {message.Correlation.MessageId}" ]
                    let canonical =
                        match message.CanonicalPayload, message.CanonicalPayloadDigest with
                        | None, None -> []
                        | Some bytes, Some digest when Canonical.verifyDigest digest bytes -> []
                        | Some _, Some _ -> [ $"Canonical payload digest mismatch: {message.Correlation.MessageId}" ]
                        | _ -> [ $"Canonical payload and digest must be supplied together: {message.Correlation.MessageId}" ]
                    raw @ canonical
                | Registry _ -> []
                | Observation observation ->
                    if Canonical.verifyDigest observation.ObservationDigest observation.Payload then []
                    else [ "Observation digest mismatch: " + string observation.Kind ])
        let bundleError =
            if Canonical.evidenceBundleDigest bundle = bundle.BundleDigest then []
            else [ "Evidence bundle digest mismatch" ]
        itemErrors @ bundleError

    let checkRequirements
        (requirements: EvidenceRequirement list)
        (bundle: EvidenceBundle)
        =
        requirements
        |> List.collect (fun (requirement: EvidenceRequirement) ->
            let matching =
                bundle.Items
                |> List.filter (kindMatches requirement.Kind)
            let trusted =
                matching
                |> List.filter (fun item ->
                    trustRank (itemTrust item) >= trustRank requirement.Trust)
            let cardinalitySatisfied =
                match requirement.Cardinality with
                | ExactlyOne -> matching.Length = 1
                | AtLeastOne -> matching.Length >= 1
                | ZeroOrMore -> true
            [
                if not cardinalitySatisfied then
                    { RequirementId = requirement.RequirementId
                      Reason =
                        match requirement.Cardinality with
                        | ExactlyOne -> $"Expected exactly one item; observed {matching.Length}"
                        | AtLeastOne -> "Expected at least one item"
                        | ZeroOrMore -> "Unreachable" }
                elif trusted.Length <> matching.Length then
                    { RequirementId = requirement.RequirementId
                      Reason = $"Trust requirement not met: {requirement.Trust}" }
            ])

[<RequireQualifiedAccess>]
module Engine =
    let resolveEvaluator (registry: EvaluatorRegistry) (definition: ObligationDefinition) =
        match Map.tryFind definition.EvaluatorId registry with
        | Some evaluator -> Ok evaluator
        | None -> Error ("Evaluator not registered: " + EvaluatorId.value definition.EvaluatorId)

    let private missingFromFacts paths =
        paths
        |> List.map (fun path ->
            { RequirementId = FactPath.value path
              Reason = "Applicability could not be determined" })
        |> NonEmptyList.ofList
        |> Result.defaultWith invalidOp

    let private validateFindings definition bundle verdict =
        match verdict with
        | Violated findings ->
            let allowedAuthorities =
                definition.Authority.ClauseId
                :: (definition.SupportingAuthorities |> List.map _.ClauseId)
                |> Set.ofList
            let evidence =
                bundle.Items
                |> List.choose (function
                    | Message message -> Some message.RawPayloadDigest
                    | Registry registry -> Some registry.ObservationDigest
                    | Observation observation -> Some observation.ObservationDigest)
                |> Set.ofList
            let invalid =
                findings
                |> NonEmptyList.toList
                |> List.tryFind (fun finding ->
                    finding.RuleId <> definition.RuleId
                    || not (Set.contains finding.Authority allowedAuthorities)
                    || finding.Evidence |> List.exists (fun reference -> not (Set.contains reference.EvidenceDigest evidence))
                    || finding.Evidence
                       |> List.exists (fun reference ->
                           reference.JsonPath
                           |> Option.exists (fun path -> not (path.StartsWith("$", StringComparison.Ordinal)))))
            match invalid with
            | Some _ -> ToolFailure "Evaluator returned an invalid or foreign finding"
            | None -> verdict
        | _ -> verdict

    let assess
        (registry: EvaluatorRegistry)
        (context: ApplicabilityContext)
        (definition: ObligationDefinition)
        (bundle: EvidenceBundle)
        =
        match Validation.definition definition with
        | error :: _ -> CompletedAssessment (ToolFailure ("Invalid admitted definition: " + error)), None
        | [] ->
            match Evidence.verifyIntegrity bundle with
            | error :: _ -> CompletedAssessment (ToolFailure error), None
            | [] ->
                match resolveEvaluator registry definition with
                | Error error -> CompletedAssessment (ToolFailure error), None
                | Ok registered ->
                    match Applicability.evaluate definition.Applicability context with
                    | NotApplicable reason -> NotApplicableAssessment reason, Some registered.Identity
                    | ApplicabilityUndetermined missing ->
                        CompletedAssessment (Inconclusive (missingFromFacts missing)), Some registered.Identity
                    | Applicable ->
                        match Evidence.checkRequirements definition.RequiredEvidence bundle with
                        | head :: tail ->
                            CompletedAssessment (Inconclusive (NonEmptyList.create head tail)), Some registered.Identity
                        | [] ->
                            let verdict =
                                try registered.Evaluate bundle
                                with error -> ToolFailure ("Evaluator threw unexpectedly: " + error.Message)
                            CompletedAssessment (validateFindings definition bundle verdict), Some registered.Identity

    let aggregate results =
        let toolFailure =
            results
            |> List.tryPick (fun result ->
                match result.Assessment with
                | CompletedAssessment (ToolFailure error) -> Some error
                | _ -> None)
        match toolFailure with
        | Some error -> ProfileToolFailure error
        | None ->
            let missing =
                results
                |> List.collect (fun result ->
                    match result.Assessment with
                    | CompletedAssessment (Inconclusive values) -> NonEmptyList.toList values
                    | _ -> [])
            match missing with
            | head :: tail -> InconclusiveProfile (NonEmptyList.create head tail)
            | [] ->
                let findings =
                    results
                    |> List.collect (fun result ->
                        match result.Assessment with
                        | CompletedAssessment (Violated values) -> NonEmptyList.toList values
                        | _ -> [])
                match findings with
                | head :: tail -> Fail (NonEmptyList.create head tail)
                | [] ->
                    let applicable =
                        results
                        |> List.filter (fun result ->
                            match result.Assessment with
                            | CompletedAssessment Satisfied -> true
                            | _ -> false)
                    if List.isEmpty applicable then
                        InconclusiveProfile (
                            NonEmptyList.create
                                { RequirementId = "applicable-rule"
                                  Reason = "No applicable rule was satisfied" }
                                []
                        )
                    else Pass

    let evaluatePack registry admission pack context bundle =
        let packDigest = Canonical.rulePackDigest pack
        if admission.Decision <> Admitted || admission.RulePackDigest <> packDigest then
            Error "Rule-pack admission is absent or does not bind this pack"
        else
            let errors = Validation.pack registry pack
            if not (List.isEmpty errors) then Error (String.concat "; " errors)
            elif context.FactsDigest <> Canonical.factsDigest context.Facts then
                Error "Applicability facts digest mismatch"
            else
                let results =
                    pack.Obligations
                    |> List.map (fun definition ->
                        let assessment, identity = assess registry context definition bundle
                        { RuleId = definition.RuleId
                          DefinitionDigest = Canonical.obligationDigest definition
                          EvaluatorIdentity =
                            identity
                            |> Option.defaultValue (
                                registry
                                |> Map.tryFind definition.EvaluatorId
                                |> Option.map _.Identity
                                |> Option.defaultWith (fun () ->
                                    invalidOp "Pack validation resolved every evaluator")
                            )
                          Assessment = assessment })
                Ok {
                    Pack = pack
                    PackDigest = packDigest
                    Admission = admission
                    Results = results
                    Verdict = aggregate results
                    Evidence = bundle
                    Facts = context
                }
