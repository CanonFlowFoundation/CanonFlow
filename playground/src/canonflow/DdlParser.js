
import { safeHash, equals, disposeSafe, isDisposable, getEnumerator } from "./fable_modules/fable-library-js.5.6.0/Util.js";
import { match, matches as matches_1 } from "./fable_modules/fable-library-js.5.6.0/RegExp.js";
import { startsWith, split } from "./fable_modules/fable-library-js.5.6.0/String.js";
import { indexOf, setItem, item } from "./fable_modules/fable-library-js.5.6.0/Array.js";
import { tryFind, toList } from "./fable_modules/fable-library-js.5.6.0/Seq.js";
import { singleton, append, empty } from "./fable_modules/fable-library-js.5.6.0/List.js";
import { TableDef, TableType, ColumnDef } from "./Canon.Introspect/SchemaProvider.js";

export function parseSql(sql) {
    const tables = [];
    const checkRegex = /CHECK\s*\((.*)\)/gui;
    const enumerator = getEnumerator(matches_1(/CREATE TABLE\s+(?:(\w+)\.)?(\w+)\s*\(([\s\S]*?)\);/gui, sql));
    try {
        while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
            const m = enumerator["System.Collections.IEnumerator.get_Current"]();
            const schema = (m[1] != null) ? (m[1] || "") : "public";
            const tableName = m[2] || "";
            const columns = [];
            const lines = split(m[3] || "", [",", "\n"], undefined, 1);
            for (let idx = 0; idx <= (lines.length - 1); idx++) {
                const line = item(idx, lines);
                const line_1 = line.trim();
                if ((!startsWith(line_1, "CONSTRAINT", 5) && !startsWith(line_1, "PRIMARY KEY", 5)) && !startsWith(line_1, "CHECK", 5)) {
                    const cMatch = match(/^([a-zA-Z0-9_]+)\s+([a-zA-Z0-9_\s]+)/gu, line_1);
                    if (cMatch != null) {
                        const colName = cMatch[1] || "";
                        const dataType = (cMatch[2] || "").trim();
                        const checks = [];
                        const chkMatch = match(checkRegex, line_1);
                        if (chkMatch != null) {
                            void (checks.push(chkMatch[1] || ""));
                        }
                        void (columns.push(new ColumnDef(colName, dataType, !(line_1.indexOf("NOT NULL") >= 0), line_1.indexOf("PRIMARY KEY") >= 0, undefined, false, undefined, undefined, toList(checks), empty(), undefined)));
                    }
                }
                else if (line_1.startsWith("CONSTRAINT") ? true : line_1.startsWith("CHECK")) {
                    const chkMatch_1 = match(checkRegex, line_1);
                    if (chkMatch_1 != null) {
                        const content = chkMatch_1[1] || "";
                        const targetCol = tryFind((c) => (content.indexOf(c.Name) >= 0), columns);
                        if (targetCol == null) {
                        }
                        else {
                            const c_1 = targetCol;
                            setItem(columns, indexOf(columns, c_1, undefined, undefined, {
                                Equals: equals,
                                GetHashCode: (x) => (safeHash(x) | 0),
                            }), new ColumnDef(c_1.Name, c_1.DataType, c_1.IsNullable, c_1.IsPrimaryKey, c_1.DefaultValue, c_1.IsGenerated, c_1.Description, c_1.MaxLength, append(c_1.CheckConstraints, singleton(content)), c_1.ParsedConstraints, c_1.Semantics));
                        }
                    }
                }
            }
            void (tables.push(new TableDef(schema, tableName, new TableType(0, []), undefined, toList(columns), empty(), empty(), empty(), empty())));
        }
    }
    finally {
        if (isDisposable(enumerator)) {
            disposeSafe(enumerator);
        }
    }
    return toList(tables);
}

