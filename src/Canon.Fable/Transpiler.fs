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
            | MaxLength len -> $"value.length <= {len}", Fidelity.Exact
            | InList items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"[{arr}].includes(value)", Fidelity.Exact
            | PrimaryKey -> "true", Fidelity.Unsupported "PrimaryKey concept does not exist in TS validators"
            | NonEmpty -> $"value.length > 0", Fidelity.Exact
            | Opaque raw -> "true /* opaque sql */", Fidelity.Unsupported $"Cannot transpile raw SQL: {raw}"
            | FieldBound(field, inner) -> 
                let innerExpr, innerF = toTypeScript (Lattice.Leaf inner)
                innerExpr.Replace("value", $"value.{field}"), innerF

    /// Emits a full TypeScript validation function and its Fidelity grade.
    let emitValidator (name: string) (predicate: Lattice<Constraint>) : string * Fidelity =
        let expr, fidelity = toTypeScript predicate
        let code = $"""export function validate_{name}(value: any): boolean {{
    return {expr};
}}"""
        code, fidelity
