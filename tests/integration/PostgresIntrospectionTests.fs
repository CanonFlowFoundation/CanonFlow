namespace Canon.IntegrationTests

open System
open System.Threading.Tasks
open Xunit
open Testcontainers.PostgreSql
open Npgsql
open Canon.Introspect
open Canon.Introspect.Postgres

type PostgresIntrospectionTests() =

    [<Fact>]
    member this.``Harvest extracts correct TableDef from live Postgres``() =
        task {
            // 1. Spin up a real PostgreSQL container
            use container = PostgreSqlBuilder().WithImage("postgres:15-alpine").Build()
            do! container.StartAsync()

            let connStr = container.GetConnectionString()
            
            // 2. Create a test schema
            use conn = new NpgsqlConnection(connStr)
            do! conn.OpenAsync()
            
            use cmd = new NpgsqlCommand(@"
                CREATE TABLE users (
                    id SERIAL PRIMARY KEY,
                    username VARCHAR(50) NOT NULL,
                    age INT CHECK (age > 0)
                );
            ", conn)
            let! _ = cmd.ExecuteNonQueryAsync()

            // 3. Introspect
            let provider = PostgresSchemaProvider(connStr) :> ISchemaProvider
            let tables = provider.Harvest()

            // 4. Assert against the Domain
            let usersTableOpt = tables |> List.tryFind (fun t -> t.Name = "users")
            Assert.True(usersTableOpt.IsSome, "Expected to harvest the 'users' table")
            
            let usersTable = usersTableOpt.Value
            
            // Check username column (should have MaxLength 50)
            let usernameCol = usersTable.Columns |> List.find (fun c -> c.Name = "username")
            Assert.False(usernameCol.IsNullable, "username should be NOT NULL")
            Assert.Equal(Some 50, usernameCol.MaxLength)
            
            // Check age column constraints
            let ageCol = usersTable.Columns |> List.find (fun c -> c.Name = "age")
            Assert.True(ageCol.CheckConstraints.Length > 0, "age should have a CHECK constraint parsed")
            Assert.Contains("> 0", ageCol.CheckConstraints.Head)
            
            do! container.StopAsync()
        }

    [<Fact>]
    member this.``The Law: Introspect(Emit(Domain)) == Domain``() =
        task {
            // 1. The pure domain definition
            let domainSchema = {
                Schema = "public"
                Name = "products"
                Type = Canon.Introspect.TableType.Table
                Description = None
                PrimaryKeys = []
                ForeignKeys = []
                Indexes = []
                TableConstraints = []
                Columns = [
                    { Name = "id"; DataType = "integer"; IsNullable = false; IsPrimaryKey = true; DefaultValue = None; IsGenerated = false; Description = None; MaxLength = None; CheckConstraints = []; ParsedConstraints = []; Semantics = None }
                    { Name = "sku"; DataType = "character varying"; IsNullable = false; IsPrimaryKey = false; DefaultValue = None; IsGenerated = false; Description = None; MaxLength = Some 20; CheckConstraints = []; ParsedConstraints = []; Semantics = None }
                    { Name = "price"; DataType = "integer"; IsNullable = false; IsPrimaryKey = false; DefaultValue = None; IsGenerated = false; Description = None; MaxLength = None; CheckConstraints = ["price > 0"]; ParsedConstraints = []; Semantics = None }
                ]
            }

            // 2. Emit DDL
            let emitter = Canon.Emit.Postgres.PostgresEmitter() :> Canon.Emit.IEmitter
            let ddlList = emitter.Emit([domainSchema])
            let ddl, _ = ddlList.Head

            // 3. Apply DDL to live DB
            use container = PostgreSqlBuilder().WithImage("postgres:15-alpine").Build()
            do! container.StartAsync()
            let connStr = container.GetConnectionString()
            
            use conn = new NpgsqlConnection(connStr)
            do! conn.OpenAsync()
            use cmd = new NpgsqlCommand(ddl, conn)
            let! _ = cmd.ExecuteNonQueryAsync()

            // 4. Introspect back
            let provider = PostgresSchemaProvider(connStr) :> ISchemaProvider
            let harvestedTables = provider.Harvest()
            let harvestedProducts = harvestedTables |> List.find (fun t -> t.Name = "products")

            // 5. Assert The Law
            Assert.Equal(domainSchema.Name, harvestedProducts.Name)
            Assert.Equal(domainSchema.Columns.Length, harvestedProducts.Columns.Length)
            
            let priceCol = harvestedProducts.Columns |> List.find (fun c -> c.Name = "price")
            Assert.True(priceCol.CheckConstraints.Head.Contains("price > 0"), "Check constraint should be symmetrically recovered")

            do! container.StopAsync()
        }
