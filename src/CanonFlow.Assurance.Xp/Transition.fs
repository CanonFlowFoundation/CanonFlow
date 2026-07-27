namespace CanonFlow.Assurance.Xp

type NonEmptyList<'T> = private { Head: 'T; Tail: 'T list }
module NonEmptyList =
    let create head tail = { Head = head; Tail = tail }
    let singleton item = { Head = item; Tail = [] }
    let toList ne = ne.Head :: ne.Tail

type Finding = { Message: string }

type TransitionDecision<'stage> =
    | Allowed of next: 'stage * requiredEvidenceKinds: Set<string>
    // XR-4: Rejected payloads are non-empty. An empty rejection is unrepresentable.
    | Rejected of NonEmptyList<Finding>
