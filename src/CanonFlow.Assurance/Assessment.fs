namespace CanonFlow.Assurance

module Assessment =
    
    let summarize (health: EvidenceHealth) (applicable: Truth list) =
        match applicable with
        | [] -> 
            // Fix: Broken + [] = ToolFailure
            match health with
            | EvidenceHealth.Complete -> Verdict.Inconclusive
            | EvidenceHealth.Partial _ -> Verdict.Inconclusive
            | EvidenceHealth.Broken _ -> Verdict.ToolFailure
        | outcomes ->
            outcomes
            |> List.fold
                (fun acc t -> Verdict.join acc (Truth.toVerdict t))
                (EvidenceHealth.toVerdict health)
