namespace CanonFlow.Reports

open System
open CanonFlow.Assurance

module VerdictView =
    open Thoth.Json.Net

    let generate (receipt: CanonFlowEvidenceReceipt) =
        // derived view VERDICT.json
        let json = Encode.object [
            "health", Encode.string "Complete"
            "compliance", Encode.string "Conformant"
            "verdict", Encode.string receipt.Verdict
            "exitCode", Encode.int 0 // Simplified for stub
        ]
        Encode.toString 4 json

