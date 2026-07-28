
import { StringBuilder__AppendLine, StringBuilder__AppendLine_Z721C83C5, StringBuilder_$ctor } from "../fable_modules/fable-library-js.5.6.0/System.Text.js";
import { disposeSafe, getEnumerator } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { join, concat } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { singleton, isEmpty, map } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { toString } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { Fidelity } from "../Canon.Core/Lineage.js";
import { Emitter } from "./Emitter.js";

export function createEmitter() {
    return new Emitter((tables) => {
        const sb = StringBuilder_$ctor();
        const enumerator = getEnumerator(tables);
        try {
            while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
                const table = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]();
                StringBuilder__AppendLine_Z721C83C5(sb, concat("CREATE TABLE ", table.Name, " ("));
                StringBuilder__AppendLine_Z721C83C5(sb, join(",\n", map((col) => {
                    let matchValue;
                    const nullability = col.IsNullable ? "NULL" : "NOT NULL";
                    return `    ${col.Name} ${(matchValue = col.MaxLength, (matchValue == null) ? col.DataType : (`${col.DataType}(${matchValue})`))} ${nullability}${isEmpty(col.CheckConstraints) ? "" : concat(" CHECK (", join(" AND ", col.CheckConstraints), ")")}`;
                }, table.Columns)));
                StringBuilder__AppendLine_Z721C83C5(sb, ");");
                StringBuilder__AppendLine(sb);
            }
        }
        finally {
            disposeSafe(enumerator);
        }
        return singleton([toString(sb), new Fidelity(0, [])]);
    });
}

