namespace Canon.Gauntlet

open Xunit
open System
open System.Diagnostics
open Canon.Introspect.Postgres
open Testcontainers.PostgreSql
open Npgsql

module ScaleTests =

    [<Fact>]
    let ``Introspect 500 tables and 5000 columns in bounded time`` () =
        let container = (new PostgreSqlBuilder())
                            .WithImage("postgres:15-alpine")
                            .WithDatabase("scale_test")
                            .WithUsername("postgres")
                            .WithPassword("postgres")
                            .Build()
        container.StartAsync().GetAwaiter().GetResult()
        
        let connStr = container.GetConnectionString()
        use conn = new NpgsqlConnection(connStr)
        conn.Open()
        
        // Generate 500 tables with 10 columns each
        use cmd = conn.CreateCommand()
        let sb = System.Text.StringBuilder()
        for t in 1 .. 500 do
            sb.AppendLine($"CREATE TABLE huge_table_{t} (") |> ignore
            sb.AppendLine("  id SERIAL PRIMARY KEY,") |> ignore
            for c in 1 .. 9 do
                sb.AppendLine($"  col_{c} VARCHAR(255) DEFAULT 'test',") |> ignore
            sb.AppendLine("  CONSTRAINT chk_" + string t + " CHECK (id > 0)") |> ignore
            sb.AppendLine(");") |> ignore
            
        cmd.CommandText <- sb.ToString()
        cmd.ExecuteNonQuery() |> ignore
        
        // Act
        let sw = Stopwatch.StartNew()
        let provider = PostgresSchemaProvider(connStr) :> Canon.Introspect.ISchemaProvider
        let tables = provider.Harvest()
        sw.Stop()
        
        container.StopAsync().GetAwaiter().GetResult()
        
        // Assert
        Assert.Equal(500, tables.Length)
        Assert.True(sw.ElapsedMilliseconds < 5000L, sprintf "Harvest took too long: %d ms" sw.ElapsedMilliseconds)
