namespace Canon.Fable

open Canon.Core

/// Transpiles the Lattice of constraints into TypeScript validation logic.
module Transpiler =
    
    let rec toTypeScript (predicate: Lattice<Constraint>) : string =
        match predicate with
        | Lattice.True -> "true"
        | Lattice.False -> "false"
        | Lattice.Not inner -> 
            $"!(%s{toTypeScript inner})"
        | Lattice.And(left, right) -> 
            $"(%s{toTypeScript left} && %s{toTypeScript right})"
        | Lattice.Or(left, right) -> 
            $"(%s{toTypeScript left} || %s{toTypeScript right})"
        | Lattice.Leaf c ->
            match c with
            | Range (Some (Exclusive v), None) -> $"value > {v}"
            | Range (None, Some (Exclusive v)) -> $"value < {v}"
            | Range (Some (Inclusive v), None) -> $"value >= {v}"
            | Range (None, Some (Inclusive v)) -> $"value <= {v}"
            | Range _ -> "true" // Add other range patterns as needed
            | IntRange _ -> "true"
            | MaxLength len -> $"value.length <= {len}"
            | InList items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"[{arr}].includes(value)"
            | PrimaryKey -> "true"
            | NonEmpty -> $"value.length > 0"

    /// Emits a full TypeScript validation function for a given named field and predicate.
    let emitValidator (name: string) (predicate: Lattice<Constraint>) : string =
        let expr = toTypeScript predicate
        $"""export function validate_{name}(value: any): boolean {{
    return {expr};
}}"""
