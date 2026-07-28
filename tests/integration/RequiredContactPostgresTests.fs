namespace Canon.IntegrationTests

open System
open System.Threading.Tasks
open Xunit
open Testcontainers.PostgreSql
open Npgsql
open Canon.Introspect
open CanonFlow.Profile.Pgsql.Experimental

type RequiredContactPostgresTests() =
    [<Fact>]
    member _.``Four-row Contact truth table agrees with PostgreSQL``() =
        task {
            use container =
                PostgreSqlBuilder()
                    .WithImage("postgres:15-alpine")
                    .Build()
            do! container.StartAsync()
            use connection =
                new NpgsqlConnection(container.GetConnectionString())
            do! connection.OpenAsync()

            use create =
                new NpgsqlCommand(
                    """
                    CREATE TABLE contact_probe (
                        email text NULL,
                        phone text NULL,
                        CONSTRAINT contact_required
                            CHECK (email IS NOT NULL OR phone IS NOT NULL)
                    );
                    """,
                    connection
                )
            let! _ = create.ExecuteNonQueryAsync()

            let rows = [
                { Email = None; Phone = None }
                { Email = Some "a@example.test"; Phone = None }
                { Email = None; Phone = Some "+91-555-0100" }
                { Email = Some "a@example.test"; Phone = Some "+91-555-0100" }
            ]

            for index, dto in rows |> List.indexed do
                let modelAdmits = Contact.decode dto |> Result.isOk
                use insert =
                    new NpgsqlCommand(
                        "INSERT INTO contact_probe(email, phone) VALUES (@email, @phone);",
                        connection
                    )
                insert.Parameters.AddWithValue(
                    "email",
                    dto.Email
                    |> Option.map box
                    |> Option.defaultValue DBNull.Value
                )
                |> ignore
                insert.Parameters.AddWithValue(
                    "phone",
                    dto.Phone
                    |> Option.map box
                    |> Option.defaultValue DBNull.Value
                )
                |> ignore
                let! postgresAdmits =
                    task {
                        try
                            let! _ = insert.ExecuteNonQueryAsync()
                            return true
                        with
                        | :? PostgresException as error
                            when error.SqlState = PostgresErrorCodes.CheckViolation ->
                            return false
                    }
                Assert.Equal(
                    modelAdmits,
                    postgresAdmits
                )
                if index = 0 then Assert.False(postgresAdmits)
                else Assert.True(postgresAdmits)

            let rowConstraint =
                SqlParser.parseRowConstraint
                    "(email IS NOT NULL OR phone IS NOT NULL)"
            match RequiredOrRecognizer.recognize rowConstraint with
            | RequiredOrRecognition.Recognized pattern ->
                Assert.Equal(
                    ("email", "phone"),
                    RequiredOrPattern.columns pattern
                )
            | result ->
                Assert.Fail($"PostgreSQL source was not recognized: {result}.")

            do! container.StopAsync()
        }
