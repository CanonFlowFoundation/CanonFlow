namespace CanonFlow.Assurance

open System
open CanonFlow.Core
open CanonFlow.Core.Verification

type Digest = Digest of string

type LedgerEvent =
    | Genesis of workflowId: string * workItemDigest: string * policyDigest: string
    | Step of name: string * payload: string

type LedgerEntry = {
    PreviousDigest: Digest option
    Event: LedgerEvent
    Digest: Digest
}

module Ledger =

    // XR-12: Hash-chain inputs use unambiguous framing via JCS
    let hashEvent (previous: Digest option) (event: LedgerEvent) : Digest =
        let prevJson = 
            match previous with 
            | Some (Digest d) -> JString d 
            | None -> JNull

        let eventJson =
            match event with
            | Genesis (wId, wDigest, pDigest) ->
                JObject [
                    "type", JString "Genesis"
                    "workflowId", JString wId
                    "workItemDigest", JString wDigest
                    "policyDigest", JString pDigest
                ]
            | Step (name, payload) ->
                JObject [
                    "type", JString "Step"
                    "name", JString name
                    "payload", JString payload
                ]

        // Frame unambiguously using our JCS implementation
        let framed = JObject [
            "previous", prevJson
            "event", eventJson
        ]

        let serialized = CanonicalJson.serialize framed
        Digest (Hash.computeSha256 serialized)

    let createGenesis workflowId workItemDigest policyDigest =
        let evt = Genesis(workflowId, workItemDigest, policyDigest)
        let digest = hashEvent None evt
        { PreviousDigest = None; Event = evt; Digest = digest }

    let appendStep (ledger: LedgerEntry list) name payload =
        match ledger |> List.tryLast with
        | None -> Error "Cannot append to empty ledger" // FsAssay-Ignore (Fixed failwith)
        | Some last ->
            let evt = Step(name, payload)
            let digest = hashEvent (Some last.Digest) evt
            Ok (ledger @ [ { PreviousDigest = Some last.Digest; Event = evt; Digest = digest } ])

    // XR-14: Monotonicity. Evidence is strictly tied to immutable ledger states. 
    // Since the ledger is append-only list of entries (values), mutation is impossible by construction.
