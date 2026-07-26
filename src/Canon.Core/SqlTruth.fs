namespace Canon.Core

/// PostgreSQL truth. Never collapsed to bool before the acceptance test.
[<RequireQualifiedAccess>]
type SqlTruth =
    | True
    | Unknown
    | False

module SqlTruth =

    let negate = function
        | SqlTruth.True    -> SqlTruth.False
        | SqlTruth.False   -> SqlTruth.True
        | SqlTruth.Unknown -> SqlTruth.Unknown

    let conj a b =
        match a, b with
        | SqlTruth.False, _   | _, SqlTruth.False   -> SqlTruth.False
        | SqlTruth.Unknown, _ | _, SqlTruth.Unknown -> SqlTruth.Unknown
        | SqlTruth.True, SqlTruth.True              -> SqlTruth.True

    let disj a b =
        match a, b with
        | SqlTruth.True, _    | _, SqlTruth.True    -> SqlTruth.True
        | SqlTruth.Unknown, _ | _, SqlTruth.Unknown -> SqlTruth.Unknown
        | SqlTruth.False, SqlTruth.False            -> SqlTruth.False

    /// Law 3. The ONLY SqlTruth -> bool in the codebase.
    let admits = function
        | SqlTruth.True | SqlTruth.Unknown -> true
        | SqlTruth.False                   -> false

    /// Comparisons propagate Unknown; IS NULL never does.
    let compare3 (f: 'a -> 'a -> bool) (l: 'a option) (r: 'a option) =
        match l, r with
        | Some a, Some b -> if f a b then SqlTruth.True else SqlTruth.False
        | _              -> SqlTruth.Unknown

    let isNull (v: 'a option) = if Option.isNone v then SqlTruth.True else SqlTruth.False
