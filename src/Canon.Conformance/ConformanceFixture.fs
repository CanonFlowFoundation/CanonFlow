namespace Canon.Conformance

open Canon.Introspect

/// A fixture that a community developer implements to prove their 
/// SchemaProvider (e.g. MySQL, SQLite) passes the CanonFlow standards.
type ConformanceFixture = {
    /// Spins up the database (e.g. via Testcontainers) and creates a standardized "Northwind-style" 
    /// test schema with PKs, FKs, Check Constraints, Enums, and Nullable fields.
    SetupTestSchema: unit -> unit
    
    /// Returns the instantiated SchemaProvider pointing to the test DB.
    GetProvider: unit -> SchemaProvider
    
    /// Cleans up the database container after tests.
    Teardown: unit -> unit
}
