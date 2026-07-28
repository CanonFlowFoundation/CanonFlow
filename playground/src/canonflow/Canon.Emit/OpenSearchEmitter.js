
import { map } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { join, concat } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { Fidelity, Divergence } from "../Canon.Core/Lineage.js";
import { Emitter } from "./Emitter.js";

export function mapDataType(sqlType) {
    const matchValue = sqlType.toLowerCase();
    switch (matchValue) {
        case "integer":
        case "int":
            return "integer";
        case "bigint":
            return "long";
        case "boolean":
            return "boolean";
        case "timestamp":
        case "date":
            return "date";
        case "decimal":
        case "numeric":
            return "double";
        default:
            return "keyword";
    }
}

export function createEmitter() {
    return new Emitter((tables) => map((table) => [concat("{\n  \"mappings\": {\n    \"properties\": {\n", join(",\n", map((col) => (`        "${col.Name}": { "type": "${mapDataType(col.DataType)}" }`), table.Columns)), "\n    }\n  }\n}"), new Fidelity(2, [new Divergence(1, ["OpenSearch drops foreign keys, constraints, and defaults"])])], tables));
}

