namespace Canon.Emit

open Canon.Core
open Canon.Introspect

/// Abstraction for database drivers to emit DDL from a TableDef schema.
/// Converts the domain representation back to storage structures.
type IEmitter =
    /// Generates DDL strings for the given Table definitions.
    abstract member Emit: TableDef list -> string list
