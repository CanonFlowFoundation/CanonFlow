
import { concat, printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { StringBuilder__AppendLine_Z721C83C5, StringBuilder_$ctor } from "../fable_modules/fable-library-js.5.6.0/System.Text.js";
import { disposeSafe, getEnumerator } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { isEmpty } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { SemanticOptimizer_simplify } from "../Canon.Core/Lattice.js";
import { toString } from "../fable_modules/fable-library-js.5.6.0/Types.js";

function formatLattice(l) {
    let matchResult, f, min, f_1, max, f_2, min_1, f_3, max_1, f_4, max_2, min_2, sql, a, b, a_1, b_1, a_2;
    switch (l.tag) {
        case 1: {
            matchResult = 1;
            break;
        }
        case 2: {
            switch (l.fields[0].tag) {
                case 12: {
                    if (l.fields[0].fields[1].tag === 0) {
                        if (l.fields[0].fields[1].fields[0] == null) {
                            if (l.fields[0].fields[1].fields[1] != null) {
                                if (l.fields[0].fields[1].fields[1].tag === 1) {
                                    matchResult = 5;
                                    f_3 = l.fields[0].fields[0];
                                    max_1 = l.fields[0].fields[1].fields[1].fields[0];
                                }
                                else {
                                    matchResult = 3;
                                    f_1 = l.fields[0].fields[0];
                                    max = l.fields[0].fields[1].fields[1].fields[0];
                                }
                            }
                            else {
                                matchResult = 11;
                            }
                        }
                        else if (l.fields[0].fields[1].fields[0].tag === 1) {
                            if (l.fields[0].fields[1].fields[1] == null) {
                                matchResult = 4;
                                f_2 = l.fields[0].fields[0];
                                min_1 = l.fields[0].fields[1].fields[0].fields[0];
                            }
                            else {
                                matchResult = 11;
                            }
                        }
                        else if (l.fields[0].fields[1].fields[1] != null) {
                            if (l.fields[0].fields[1].fields[1].tag === 0) {
                                matchResult = 6;
                                f_4 = l.fields[0].fields[0];
                                max_2 = l.fields[0].fields[1].fields[1].fields[0];
                                min_2 = l.fields[0].fields[1].fields[0].fields[0];
                            }
                            else {
                                matchResult = 11;
                            }
                        }
                        else {
                            matchResult = 2;
                            f = l.fields[0].fields[0];
                            min = l.fields[0].fields[1].fields[0].fields[0];
                        }
                    }
                    else {
                        matchResult = 11;
                    }
                    break;
                }
                case 9: {
                    matchResult = 7;
                    sql = l.fields[0].fields[0];
                    break;
                }
                default:
                    matchResult = 11;
            }
            break;
        }
        case 4: {
            matchResult = 8;
            a = l.fields[0];
            b = l.fields[1];
            break;
        }
        case 5: {
            matchResult = 9;
            a_1 = l.fields[0];
            b_1 = l.fields[1];
            break;
        }
        case 3: {
            matchResult = 10;
            a_2 = l.fields[0];
            break;
        }
        default:
            matchResult = 0;
    }
    switch (matchResult) {
        case 0:
            return "Any";
        case 1:
            return "None";
        case 2:
            return toText(printf("%s >= %M"))(f)(min);
        case 3:
            return toText(printf("%s <= %M"))(f_1)(max);
        case 4:
            return toText(printf("%s > %M"))(f_2)(min_1);
        case 5:
            return toText(printf("%s < %M"))(f_3)(max_1);
        case 6:
            return toText(printf("%M <= %s <= %M"))(min_2)(f_4)(max_2);
        case 7:
            return sql;
        case 8: {
            const arg_11 = formatLattice(a);
            const arg_12 = formatLattice(b);
            return toText(printf("(%s AND %s)"))(arg_11)(arg_12);
        }
        case 9: {
            const arg_13 = formatLattice(a_1);
            const arg_14 = formatLattice(b_1);
            return toText(printf("(%s OR %s)"))(arg_13)(arg_14);
        }
        case 10: {
            const arg_15 = formatLattice(a_2);
            return toText(printf("NOT (%s)"))(arg_15);
        }
        default:
            return "ComplexBound";
    }
}

export function emitAgentCatalog(table) {
    const sb = StringBuilder_$ctor();
    const add = (s) => {
        StringBuilder__AppendLine_Z721C83C5(sb, s);
    };
    add("---");
    add("kind: SemanticModel");
    add("version: 1.0");
    add("metadata:");
    add(concat("  name: ", table.Name));
    add(concat("  schema: ", table.Schema));
    add(`  type: ${table.Type}`);
    add("columns:");
    const enumerator = getEnumerator(table.Columns);
    try {
        while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
            const col = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]();
            add(concat("  - name: ", col.Name));
            add(concat("    type: ", col.DataType));
            const pkStr = col.IsPrimaryKey ? "true" : "false";
            add(concat("    nullable: ", col.IsNullable ? "true" : "false"));
            add(concat("    isPrimaryKey: ", pkStr));
            if (!isEmpty(col.ParsedConstraints)) {
                add("    semanticBounds:");
                const enumerator_1 = getEnumerator(col.ParsedConstraints);
                try {
                    while (enumerator_1["System.Collections.IEnumerator.MoveNext"]()) {
                        add(concat("      - \"", formatLattice(SemanticOptimizer_simplify(enumerator_1["System.Collections.Generic.IEnumerator`1.get_Current"]())), "\""));
                    }
                }
                finally {
                    disposeSafe(enumerator_1);
                }
                add("    agentDirectives:");
                add(concat("      - \"Always ensure frontend validators restrict ", col.Name, " to these semantic bounds to avoid 500s.\""));
            }
        }
    }
    finally {
        disposeSafe(enumerator);
    }
    add("relationships:");
    const enumerator_2 = getEnumerator(table.ForeignKeys);
    try {
        while (enumerator_2["System.Collections.IEnumerator.MoveNext"]()) {
            const fk = enumerator_2["System.Collections.Generic.IEnumerator`1.get_Current"]();
            add(concat("  - column: ", fk.ColumnName));
            add(concat("    referencesTable: ", fk.RefTable));
            add(concat("    referencesColumn: ", fk.RefColumn));
        }
    }
    finally {
        disposeSafe(enumerator_2);
    }
    return toString(sb);
}

