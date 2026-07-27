namespace Canon.Emit

open Canon.Core
open Canon.Introspect

/// Abstraction for database drivers to emit DDL from a TableDef schema.
/// Converts the domain representation back to storage structures.
type Emitter = {
    /// Generates DDL strings and their Fidelity for the given Table definitions.
    Emit: TableDef list -> (string * Fidelity) list
}
