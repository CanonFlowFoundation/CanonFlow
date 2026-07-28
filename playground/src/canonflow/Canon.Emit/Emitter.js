
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { TableDef_$reflection } from "../Canon.Introspect/SchemaProvider.js";
import { record_type, lambda_type, tuple_type, string_type, list_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Fidelity_$reflection } from "../Canon.Core/Lineage.js";

/**
 * Abstraction for database drivers to emit DDL from a TableDef schema.
 * Converts the domain representation back to storage structures.
 */
export class Emitter extends Record {
    constructor(Emit) {
        super();
        this.Emit = Emit;
    }
}

export function Emitter_$reflection() {
    return record_type("Canon.Emit.Emitter", [], Emitter, () => [["Emit", lambda_type(list_type(TableDef_$reflection()), list_type(tuple_type(string_type, Fidelity_$reflection())))]]);
}

