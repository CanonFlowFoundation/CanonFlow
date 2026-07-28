namespace Canon.Core

type RowConstraint = {
    Predicate: Lattice<Constraint>
    ReferencedColumns: Set<string>
    HasOpaqueNode: bool
}

[<RequireQualifiedAccess>]
module RowConstraint =
    let private leafMetadata = function
        | FieldBound (column, Opaque _) ->
            Set.singleton column, true
        | FieldBound (column, _) ->
            Set.singleton column, false
        | RelativeBound (left, _, right) ->
            Set.ofList [left; right], false
        | Opaque _ ->
            Set.empty, true
        | _ ->
            Set.empty, false

    let rec private metadata = function
        | Lattice.True
        | Lattice.False ->
            Set.empty, false
        | Lattice.Leaf constraint_ ->
            leafMetadata constraint_
        | Lattice.Not inner ->
            metadata inner
        | Lattice.And (left, right)
        | Lattice.Or (left, right) ->
            let leftColumns, leftOpaque = metadata left
            let rightColumns, rightOpaque = metadata right
            Set.union leftColumns rightColumns, leftOpaque || rightOpaque

    let create predicate =
        let columns, hasOpaqueNode = metadata predicate
        {
            Predicate = predicate
            ReferencedColumns = columns
            HasOpaqueNode = hasOpaqueNode
        }
