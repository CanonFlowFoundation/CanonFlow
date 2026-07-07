namespace Canon.Fable

open Canon.Core

/// Transpiles the Lattice of constraints into TypeScript validation logic.
module Transpiler =
    
    let rec toTypeScript (predicate: Lattice<Constraint>) : string * Fidelity =
        match predicate with
        | Lattice.True -> "true", Fidelity.Exact
        | Lattice.False -> "false", Fidelity.Exact
        | Lattice.Not inner -> 
            let expr, f = toTypeScript inner
            $"!(%s{expr})", f
        | Lattice.And(left, right) -> 
            let lExpr, lF = toTypeScript left
            let rExpr, rF = toTypeScript right
            $"(%s{lExpr} && %s{rExpr})", Fidelity.combine lF rF
        | Lattice.Or(left, right) -> 
            let lExpr, lF = toTypeScript left
            let rExpr, rF = toTypeScript right
            $"(%s{lExpr} || %s{rExpr})", Fidelity.combine lF rF
        | Lattice.Leaf c ->
            match c with
            | Range (Some (Exclusive v), None) -> $"value > {v}", Fidelity.Exact
            | Range (None, Some (Exclusive v)) -> $"value < {v}", Fidelity.Exact
            | Range (Some (Inclusive v), None) -> $"value >= {v}", Fidelity.Exact
            | Range (None, Some (Inclusive v)) -> $"value <= {v}", Fidelity.Exact
            | Range _ -> "true", Fidelity.Approximate "Complex range bounds not fully implemented in TS"
            | IntRange _ -> "Number.isInteger(value)", Fidelity.Approximate "Int range requires precision bounds"
            | StringRange (Some (Exclusive v), None) -> $"value > '{v}'", Fidelity.Approximate "String range collation may differ"
            | StringRange (None, Some (Exclusive v)) -> $"value < '{v}'", Fidelity.Approximate "String range collation may differ"
            | StringRange (Some (Inclusive v), None) -> $"value >= '{v}'", Fidelity.Approximate "String range collation may differ"
            | StringRange (None, Some (Inclusive v)) -> $"value <= '{v}'", Fidelity.Approximate "String range collation may differ"
            | StringRange _ -> "true", Fidelity.Approximate "Complex string range bounds not fully implemented in TS"
            | MaxLength len -> $"value.length <= {len}", Fidelity.Exact
            | InList items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"[{arr}].includes(value)", Fidelity.Exact
            | InSet items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"[{arr}].includes(value)", Fidelity.Exact
            | RelativeBound(colA, op, colB) ->
                let isLiteral (s: string) = s.StartsWith("'") || System.Char.IsDigit(s.[0]) || s.StartsWith("-")
                let a = if isLiteral colA then colA else $"value.{colA}"
                let b = if isLiteral colB then colB else $"value.{colB}"
                $"{a} {op} {b}", Fidelity.Exact
            | PrimaryKey -> "true", Fidelity.Unsupported "PrimaryKey concept does not exist in TS validators"
            | NonEmpty -> $"value.length > 0", Fidelity.Exact
            | Opaque raw -> "true /* opaque sql */", Fidelity.Unsupported $"Cannot transpile raw SQL: {raw}"
            | FieldBound(field, inner) -> 
                let innerExpr, innerF = toTypeScript (Lattice.Leaf inner)
                innerExpr.Replace("value", $"value.{field}"), innerF

    /// Emits a full TypeScript validation function and its Fidelity grade.
    let emitValidator (name: string) (predicate: Lattice<Constraint>) (isNullable: bool) : string * Fidelity =
        let expr, fidelity = toTypeScript predicate
        let guard = if isNullable then "\n    if (value === null || value === undefined) return true;" else ""
        let code = $"""export function validate_{name}(value: any): boolean {{{guard}
    return {expr};
}}"""
        code, fidelity
