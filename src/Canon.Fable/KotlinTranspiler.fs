namespace Canon.Fable

open Canon.Core

/// Transpiles the Lattice of constraints into Kotlin validation logic using BigDecimal.
module KotlinTranspiler =
    
    let rec toKotlin (predicate: Lattice<Constraint>) : string * Fidelity =
        match predicate with
        | Lattice.True -> "true", Fidelity.Exact
        | Lattice.False -> "false", Fidelity.Exact
        | Lattice.Not inner -> 
            let expr, f = toKotlin inner
            $"!(%s{expr})", f
        | Lattice.And(left, right) -> 
            let lExpr, lF = toKotlin left
            let rExpr, rF = toKotlin right
            $"(%s{lExpr} && %s{rExpr})", Fidelity.combine lF rF
        | Lattice.Or(left, right) -> 
            let lExpr, lF = toKotlin left
            let rExpr, rF = toKotlin right
            $"(%s{lExpr} || %s{rExpr})", Fidelity.combine lF rF
        | Lattice.Leaf c ->
            match c with
            | Range (Some (Exclusive v), None) -> $"value > java.math.BigDecimal(\"{v}\")", Fidelity.Exact
            | Range (None, Some (Exclusive v)) -> $"value < java.math.BigDecimal(\"{v}\")", Fidelity.Exact
            | Range (Some (Inclusive v), None) -> $"value >= java.math.BigDecimal(\"{v}\")", Fidelity.Exact
            | Range (None, Some (Inclusive v)) -> $"value <= java.math.BigDecimal(\"{v}\")", Fidelity.Exact
            | Range _ -> "true", Fidelity.Approximate "Complex range bounds not fully implemented in Kotlin"
            | IntRange _ -> "value.scale() <= 0", Fidelity.Approximate "Int range check"
            | MaxLength len -> $"value.length <= {len}", Fidelity.Exact
            | InList items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"listOf({arr}).contains(value)", Fidelity.Exact
            | InSet items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"setOf({arr}).contains(value)", Fidelity.Exact
            | RelativeBound(colA, op, colB) ->
                $"value.{colA} {op} value.{colB}", Fidelity.Exact
            | PrimaryKey -> "true", Fidelity.Unsupported "PrimaryKey concept does not exist in Kotlin validators"
            | NonEmpty -> $"value.isNotEmpty()", Fidelity.Exact
            | Opaque raw -> "true /* opaque sql */", Fidelity.Unsupported $"Cannot transpile raw SQL: {raw}"
            | FieldBound(field, inner) -> 
                let innerExpr, innerF = toKotlin (Lattice.Leaf inner)
                innerExpr.Replace("value", $"value.{field}"), innerF

    /// Emits a full Kotlin validation function and its Fidelity grade.
    let emitValidator (name: string) (predicate: Lattice<Constraint>) (isNullable: bool) : string * Fidelity =
        let expr, fidelity = toKotlin predicate
        let guard = if isNullable then "\n    if (value == null) return true" else ""
        let code = $"""fun validate_{name}(value: dynamic): Boolean {{{guard}
    return {expr}
}}"""
        code, fidelity
