namespace CanonFlow.Profile.Pgsql

open System
open Npgsql
open CanonFlow.Assurance

module PgsqlAssessor =

    /// Retrieves PostgreSQL connection string from environment variables.
    let getConnectionString () =
        let host = Environment.GetEnvironmentVariable("PG_HOST") |> Option.ofObj |> Option.defaultValue "localhost"
        let user = Environment.GetEnvironmentVariable("PG_USER") |> Option.ofObj |> Option.defaultValue "evaluator"
        let pass = Environment.GetEnvironmentVariable("PG_PASSWORD") |> Option.ofObj |> Option.defaultValue "pass"
        let db = Environment.GetEnvironmentVariable("PG_DB") |> Option.ofObj |> Option.defaultValue "differential_db"
        $"Host={host};Username={user};Password={pass};Database={db}"

    /// Test Database Connection
    let checkConnection (connString: string) : Truth =
        try
            use conn = new NpgsqlConnection(connString)
            conn.Open()
            Truth.Clear ClearOutcome.Conformant
        with
        | ex -> 
            Truth.Interrupted ({ Description = $"Database startup failure: {ex.Message}" }, [])

    /// Tests that the timezone is UTC as required.
    let testTimezone (connString: string) : Truth =
        try
            use conn = new NpgsqlConnection(connString)
            conn.Open()
            use cmd = new NpgsqlCommand("SHOW TIME ZONE", conn)
            let tz = string (cmd.ExecuteScalar())
            if tz = "UTC" then
                Truth.Clear ClearOutcome.Conformant
            else
                Truth.Clear (ClearOutcome.NonConformant (NonEmpty.create { Description = $"Expected UTC timezone, got {tz}" } []))
        with
        | ex -> 
            Truth.Interrupted ({ Description = $"Query execution failed: {ex.Message}" }, [])

    /// Main evaluator function
    let evaluate (evaluationId: string) (timestamp: string) : CanonFlowEvidenceReceipt =
        let connString = getConnectionString ()

        // Perform differential tests
        let connTruth = checkConnection connString

        let health, outcomes =
            match connTruth with
            | Truth.Interrupted (fail, _) ->
                EvidenceHealth.Broken fail, [ connTruth ]
            | _ ->
                let tzTruth = testTimezone connString
                
                let rules = [ connTruth; tzTruth ]

                let h = 
                    match tzTruth with
                    | Truth.Interrupted (fail, _) -> EvidenceHealth.Broken fail
                    | _ -> EvidenceHealth.Complete

                h, rules

        let verdict = Assessment.summarize health outcomes
        let ctx : ReceiptContext = {
            Instant = timestamp
            TimeProvenance = "System-Clock"
            Locale = "invariant"
            NetworkPolicy = "isolated"
        }

        let assessmentRecord : ComponentAssessmentRecord = {
            ComponentId = "CanonFlow.Profile.Pgsql"
            ComponentVersion = "1.0.0"
            Health = health
            Compliance =
                match verdict with
                | Verdict.Pass -> Compliance.Conformant
                | Verdict.Fail -> Compliance.NonConformant (NonEmpty.create { Description = "PostgreSQL differential assessment failed." } [])
                | _ -> Compliance.NotEstablished
            ApplicableRules = outcomes.Length
            EvaluatedRules = outcomes.Length
            Evidence = []
        }

        {
            SchemaVersion = "1.0"
            ReceiptType = "CanonFlowEvidenceReceipt"
            ReplayIdentity = evaluationId
            Subject = {
                Root = evaluationId
                Schema = "PostgreSQL"
                SourceDirectories = []
                ManifestDigest = None
                Artifacts = []
            }
            Evaluator = { EngineId = "CanonFlow.Evaluator"; EngineVersion = "1.0.0" }
            Context = ctx
            Assessments = [assessmentRecord]
            Verdict = verdict
            Seal = None
        }

