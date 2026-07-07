namespace Canon.Fable

open Canon.Core

/// Transpiles the Lattice of constraints into Swift validation logic using Decimal.
module SwiftTranspiler =
    
    let rec toSwift (predicate: Lattice<Constraint>) : string * Fidelity =
        match predicate with
        | Lattice.True -> "true", Fidelity.Exact
        | Lattice.False -> "false", Fidelity.Exact
        | Lattice.Not inner -> 
            let expr, f = toSwift inner
            $"!(%s{expr})", f
        | Lattice.And(left, right) -> 
            let lExpr, lF = toSwift left
            let rExpr, rF = toSwift right
            $"(%s{lExpr} && %s{rExpr})", Fidelity.combine lF rF
        | Lattice.Or(left, right) -> 
            let lExpr, lF = toSwift left
            let rExpr, rF = toSwift right
            $"(%s{lExpr} || %s{rExpr})", Fidelity.combine lF rF
        | Lattice.Leaf c ->
            match c with
            | Range (Some (Exclusive v), None) -> $"value > Decimal(string: \"{v}\")!", Fidelity.Exact
            | Range (None, Some (Exclusive v)) -> $"value < Decimal(string: \"{v}\")!", Fidelity.Exact
            | Range (Some (Inclusive v), None) -> $"value >= Decimal(string: \"{v}\")!", Fidelity.Exact
            | Range (None, Some (Inclusive v)) -> $"value <= Decimal(string: \"{v}\")!", Fidelity.Exact
            | Range _ -> "true", Fidelity.Approximate "Complex range bounds not fully implemented in Swift"
            | IntRange _ -> "value.isSignalingNaN == false", Fidelity.Approximate "Int range check"
            | StringRange (Some (Exclusive v), None) -> $"value > \"{v}\"", Fidelity.Approximate "String range collation may differ"
            | StringRange (None, Some (Exclusive v)) -> $"value < \"{v}\"", Fidelity.Approximate "String range collation may differ"
            | StringRange (Some (Inclusive v), None) -> $"value >= \"{v}\"", Fidelity.Approximate "String range collation may differ"
            | StringRange (None, Some (Inclusive v)) -> $"value <= \"{v}\"", Fidelity.Approximate "String range collation may differ"
            | StringRange _ -> "true", Fidelity.Approximate "Complex string range bounds not fully implemented in Swift"
            | MaxLength len -> $"value.count <= {len}", Fidelity.Exact
            | InList items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"[{arr}].contains(value)", Fidelity.Exact
            | InSet items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"Set([{arr}]).contains(value)", Fidelity.Exact
            | RelativeBound(colA, op, colB) ->
                let isLiteral (s: string) = s.StartsWith("\"") || s.StartsWith("'") || System.Char.IsDigit(s.[0]) || s.StartsWith("-")
                let a = if isLiteral colA then colA else $"value.{colA}"
                let b = if isLiteral colB then colB else $"value.{colB}"
                $"{a} {op} {b}", Fidelity.Exact
            | PrimaryKey -> "true", Fidelity.Unsupported "PrimaryKey concept does not exist in Swift validators"
            | NonEmpty -> $"!value.isEmpty", Fidelity.Exact
            | Opaque raw -> "true /* opaque sql */", Fidelity.Unsupported $"Cannot transpile raw SQL: {raw}"
            | FieldBound(field, inner) -> 
                let innerExpr, innerF = toSwift (Lattice.Leaf inner)
                innerExpr.Replace("value", $"value.{field}"), innerF

    /// Emits a full Swift validation function and its Fidelity grade.
    let emitValidator (name: string) (predicate: Lattice<Constraint>) (isNullable: bool) : string * Fidelity =
        let expr, fidelity = toSwift predicate
        let guard = if isNullable then "\n    if value == nil { return true }" else ""
        let code = $"""func validate_{name}(value: Any?) -> Bool {{{guard}
    return {expr}
}}"""
        code, fidelity
