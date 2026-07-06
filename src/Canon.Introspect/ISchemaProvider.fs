namespace Canon.Introspect

open Canon.Core

/// Represents semantic metadata attached to a field (from Helios concepts)
type SemanticMetadata = {
    Label: string option
    Description: string option
    Synonyms: string list
    Metrics: string list
    FieldKind: Canon.Core.FieldKind option
}

/// Represents a column in the database schema.
type ColumnDef = {
    Name: string
    DataType: string
    IsNullable: bool
    MaxLength: int option
    // Extracted constraints which will translate to Refined<'T,'P>
    CheckConstraints: string list
    // Helios semantic catalog data
    Semantics: SemanticMetadata option
}

/// Represents a table in the database schema.
type TableDef = {
    Schema: string
    Name: string
    Columns: ColumnDef list
}

/// Abstraction for database drivers to provide their schema and constraints.
/// SqlHydra will implement this for Postgres/DuckDB/SQLite.
type ISchemaProvider =
    /// Harvests the schema from the live database.
    abstract member Harvest: unit -> TableDef list
