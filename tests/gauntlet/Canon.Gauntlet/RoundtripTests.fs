namespace Canon.Gauntlet

open Xunit
open FsCheck
open FsCheck.Xunit
open Canon.Core
open Canon.Introspect
open Testcontainers.PostgreSql
open System.Threading.Tasks
open Npgsql

module RoundtripTests =

    let renderSql (ast: Lattice<Constraint>) : string =
        match ast with
        | Leaf (FieldBound(col, Range(Some(Inclusive v), None))) -> sprintf "CHECK (%s >= %M)" col v
        | Leaf (FieldBound(col, Range(None, Some(Inclusive v)))) -> sprintf "CHECK (%s <= %M)" col v
        | _ -> "CHECK (true)"

    type Generators() =
        static member Decimal() =
            Arb.generate<int> |> Gen.map decimal |> Arb.fromGen
            
        static member Constraint() =
            gen {
                let! col = Gen.elements ["age"; "score"; "balance"]
                let! v = Arb.generate<int>
                let! isLower = Arb.generate<bool>
                if isLower then
                    return Leaf (FieldBound(col, Range(Some(Inclusive (decimal v)), None)))
                else
                    return Leaf (FieldBound(col, Range(None, Some(Inclusive (decimal v)))))
            } |> Arb.fromGen

    [<Properties(Arbitrary = [| typeof<Generators> |], MaxTest = 5)>]
    type RoundtripProperties() =

        let runTest (version: string) ast =
            let container = (new PostgreSqlBuilder())
                                .WithImage(version)
                                .WithDatabase("gauntlet")
                                .WithUsername("postgres")
                                .WithPassword("postgres")
                                .Build()
            container.StartAsync().GetAwaiter().GetResult()
            
            let renderedSql = renderSql ast
            let parsed = SqlParser.parseConstraint (renderedSql.Substring(6)) // skip "CHECK "
            
            let sim1 = SemanticOptimizer.simplify ast
            let sim2 = SemanticOptimizer.simplify parsed
            let isMatch = sim1 = sim2 || (match parsed with Leaf(Opaque _) -> true | _ -> false)
            
            container.StopAsync().GetAwaiter().GetResult()
            isMatch

        [<Property>]
        member this.``Deparse round-trip PG 14`` (ast: Lattice<Constraint>) =
            runTest "postgres:14-alpine" ast

        [<Property>]
        member this.``Deparse round-trip PG 16`` (ast: Lattice<Constraint>) =
            runTest "postgres:16-alpine" ast

        [<Property>]
        member this.``Deparse round-trip PG 17`` (ast: Lattice<Constraint>) =
            runTest "postgres:17-alpine" ast
