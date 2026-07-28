namespace CanonFlow.Assurance

/// Claim vocabulary shared by evaluator surfaces and downstream SDKs.
[<RequireQualifiedAccess>]
type ClaimKind =
    | Verified
    | ConstructivelyProjected
    | Inconclusive
    | Unsupported
    | Experimental

/// Constructive modelling is never enabled implicitly.
[<RequireQualifiedAccess>]
type ConstructiveMode =
    | Dormant
    | Experimental

[<RequireQualifiedAccess>]
module ClaimPolicy =
    let vocabulary =
        [
            ClaimKind.Verified
            ClaimKind.ConstructivelyProjected
            ClaimKind.Inconclusive
            ClaimKind.Unsupported
            ClaimKind.Experimental
        ]

    let defaultConstructiveMode = ConstructiveMode.Dormant

    let canEmitConstructiveProjection = function
        | ConstructiveMode.Dormant -> false
        | ConstructiveMode.Experimental -> true

    let text = function
        | ClaimKind.Verified -> "Verified"
        | ClaimKind.ConstructivelyProjected -> "ConstructivelyProjected"
        | ClaimKind.Inconclusive -> "Inconclusive"
        | ClaimKind.Unsupported -> "Unsupported"
        | ClaimKind.Experimental -> "Experimental"
