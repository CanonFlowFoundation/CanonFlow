namespace CanonFlow.Assurance

open System

// XR-1 (Totality), XR-2 (Purity)
module Verification =
    
    // We never throw exceptions or read from global state/clocks.
    // Time must be passed in as evidence if needed.
    
    type VerificationResult =
        | Verified of LedgerEntry list
        | Rejected of string
        | ToolFailure of string

    let verifyLedger (ledger: LedgerEntry list) : VerificationResult =
        try
            // Empty ledger is a tool failure/rejection, not a crash
            if List.isEmpty ledger then
                ToolFailure "Ledger is empty"
            else
                let rec checkChain (entries: LedgerEntry list) (expectedPrev: Digest option) =
                    match entries with
                    | [] -> true
                    | e :: rest ->
                        if e.PreviousDigest <> expectedPrev then false
                        else
                            let computed = Ledger.hashEvent e.PreviousDigest e.Event
                            if computed <> e.Digest then false
                            else checkChain rest (Some e.Digest)

                if checkChain ledger None then
                    Verified ledger
                else
                    Rejected "Hash chain broken"
        with
        // XR-1: All unexpected crashes denote ToolFailure, never crash the process.
        | ex -> ToolFailure (sprintf "Kernel panic: %s" ex.Message)
