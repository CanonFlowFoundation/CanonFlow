namespace CanonFlow.Assurance.Xp

open System

type AiPrincipal = 
    | Agent of id: string
    | TestAuthor of id: string
    | Implementer of id: string
    | Reviewer of id: string

type Capability =
    | ProposeChange
    | RunSandboxTests
    | SubmitForReview
    | AdmitSource
    | WriteAuthoritativeLedger
    | WeakenPolicy
    | AcceptOwnReview
    | SignReceipt
    | Merge
    | Release
    | HoldRunnerKey

module Capabilities =

    // XR-11: Capability disjointness
    // No AI principal may hold any of the protected capabilities.
    // Notably, HoldRunnerKey is physically isolated by the project graph,
    // but we encode the mathematical restriction here per M0.
    let protectedCapabilities = 
        Set.ofList [
            AdmitSource
            WriteAuthoritativeLedger
            WeakenPolicy
            AcceptOwnReview
            SignReceipt
            Merge
            Release
            HoldRunnerKey
        ]

    let agentCapabilities =
        Set.ofList [
            ProposeChange
            RunSandboxTests
            SubmitForReview
        ]

    let cap (principal: AiPrincipal) : Set<Capability> =
        // For any AI principal, they only get the restricted agent capabilities.
        agentCapabilities

    let isDisjoint (c1: Set<Capability>) (c2: Set<Capability>) =
        Set.intersect c1 c2 |> Set.isEmpty

    // The core law of M0
    let satisfiesXr11 (principal: AiPrincipal) =
        isDisjoint (cap principal) protectedCapabilities
