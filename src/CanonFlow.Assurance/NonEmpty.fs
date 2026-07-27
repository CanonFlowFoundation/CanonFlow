namespace CanonFlow.Assurance

type EmptySequence = | EmptySequence

type NonEmpty<'T> = private NonEmpty of head: 'T * tail: 'T list

module NonEmpty =
    let create head tail = NonEmpty(head, tail)
    let ofList xs =
        match xs with
        | [] -> Error EmptySequence
        | h :: t -> Ok (NonEmpty (h, t))

    let toList (NonEmpty (h, t)) = h :: t
    let head (NonEmpty (h, _)) = h
    let length (NonEmpty (_, t)) = 1 + List.length t
