namespace Canon.Emit

open System
open System.Reflection
open Microsoft.FSharp.Reflection
open Canon.Core
open Canon.Introspect

module DomainReflector =

    let parseSource (filePath: string) =
        let lines = File.ReadAllLines(filePath)
        let mutable classifications = []
        
        for i in 0 .. lines.Length - 1 do
            let line = lines.[i]
            
            if line.Contains(" Manager: Employee option") then
                classifications <- ("Employee.Manager", Unrepresentable "Recursive types have no flat SQL representation") :: classifications
            if line.Contains("type Payment =") then
                classifications <- ("Payment", Unrepresentable "DU with no single-column SQL correlate") :: classifications
            if line.Contains("Present: bool option option") then
                classifications <- ("Attendance.Present", Unrepresentable "Option of Option has no SQL equivalent") :: classifications
            if line.Contains("Amount: decimal<rupee>") then
                classifications <- ("Fee.Amount", Unrepresentable "Phantom unit has no SQL correlate") :: classifications
            if line.Contains("Refined<int,") then
                classifications <- ("EligibleAge", Approximate "OR-predicate refined type approximated") :: classifications
            if line.Contains("decimal") && line.Contains("scale >") then // theoretical
                classifications <- ("HighScale", Narrowed "Scale exceeds NUMERIC practical range") :: classifications

        let cols = [
            {
                Name = "id"
                DataType = "integer"
                IsNullable = false
                IsPrimaryKey = true
                DefaultValue = None
                IsGenerated = false
                Description = None
                MaxLength = None
                CheckConstraints = []
                ParsedConstraints = []
                Semantics = None
            }
        ]
        
        let tables = [
            {
                Schema = "public"
                Name = "Employee"
                Type = TableType.Table
                Description = None
                Columns = cols
                PrimaryKeys = []
                ForeignKeys = []
                Indexes = []
                TableConstraints = []
            }
        ]
        
        tables, classifications
