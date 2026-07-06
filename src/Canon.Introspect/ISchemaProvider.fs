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

type ForeignKeyDef = {
    ColumnName: string
    RefTable: string
    RefColumn: string
}

type IndexDef = {
    Name: string
    Columns: string list
    IsUnique: bool
}

/// Represents a column in the database schema.
type ColumnDef = {
    Name: string
    DataType: string
    IsNullable: bool
    IsPrimaryKey: bool
    DefaultValue: string option
    IsGenerated: bool
    Description: string option
    MaxLength: int option
    // Extracted constraints which will translate to Refined<'T,'P>
    CheckConstraints: string list
    ParsedConstraints: Canon.Core.Lattice<Canon.Core.Constraint> list
    // Helios semantic catalog data
    Semantics: SemanticMetadata option
}

type TableType = 
    | Table
    | View
    | MaterializedView

/// Represents a table or view in the database schema.
type TableDef = {
    Schema: string
    Name: string
    Type: TableType
    Description: string option
    Columns: ColumnDef list
    PrimaryKeys: string list
    ForeignKeys: ForeignKeyDef list
    Indexes: IndexDef list
    TableConstraints: string list
}

/// Abstraction for database drivers to provide their schema and constraints.
/// SqlHydra will implement this for Postgres/DuckDB/SQLite.
type ISchemaProvider =
    /// Harvests the schema from the live database.
    abstract member Harvest: unit -> TableDef list
