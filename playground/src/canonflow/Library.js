
import { concat, split, substring, trim } from "./fable_modules/fable-library-js.5.6.0/String.js";
import { tryParse } from "./fable_modules/fable-library-js.5.6.0/Decimal.js";
import Decimal from "./fable_modules/fable-library-js.5.6.0/Decimal.js";
import { item } from "./fable_modules/fable-library-js.5.6.0/Array.js";
import { toString, FSharpRef } from "./fable_modules/fable-library-js.5.6.0/Types.js";
import { SemanticOptimizer_simplify, Lattice$1, Constraint, Bound$1 } from "./Canon.Core/Lattice.js";
import { parseSql } from "./DdlParser.js";
import { equals, disposeSafe, getEnumerator } from "./fable_modules/fable-library-js.5.6.0/Util.js";
import { singleton, isEmpty, map, reduce, length } from "./fable_modules/fable-library-js.5.6.0/List.js";
import { StringBuilder__AppendLine_Z721C83C5, StringBuilder_$ctor } from "./fable_modules/fable-library-js.5.6.0/System.Text.js";
import { TableDef, ColumnDef } from "./Canon.Introspect/SchemaProvider.js";
import { emitValidator } from "./Canon.Fable/Transpiler.js";
import { emitValidator as emitValidator_1 } from "./Canon.Fable/KotlinTranspiler.js";
import { emitValidator as emitValidator_2 } from "./Canon.Fable/SwiftTranspiler.js";
import { emitGenerators } from "./Canon.Emit/FsCheckEmitter.js";

export function parseConstraintStr(s) {
    let outArg, outArg_1, outArg_2, outArg_3;
    const s_1 = s.toUpperCase().trim();
    const s_2 = s_1.startsWith("CHECK") ? trim(substring(s_1, 5), " ", "(", ")") : s_1;
    if (s_2.indexOf(">=") >= 0) {
        const p = split(s_2, [">="], undefined, 0);
        return new Lattice$1(2, [new Constraint(0, [new Bound$1(0, [((outArg = (new Decimal("0")), [tryParse(item(1, p).trim(), new FSharpRef(() => outArg, (v) => {
            outArg = v;
        })), outArg]))[1]]), undefined])]);
    }
    else if (s_2.indexOf(">") >= 0) {
        const p_1 = split(s_2, [">"], undefined, 0);
        return new Lattice$1(2, [new Constraint(0, [new Bound$1(1, [((outArg_1 = (new Decimal("0")), [tryParse(item(1, p_1).trim(), new FSharpRef(() => outArg_1, (v_1) => {
            outArg_1 = v_1;
        })), outArg_1]))[1]]), undefined])]);
    }
    else if (s_2.indexOf("<=") >= 0) {
        const p_2 = split(s_2, ["<="], undefined, 0);
        return new Lattice$1(2, [new Constraint(0, [undefined, new Bound$1(0, [((outArg_2 = (new Decimal("0")), [tryParse(item(1, p_2).trim(), new FSharpRef(() => outArg_2, (v_2) => {
            outArg_2 = v_2;
        })), outArg_2]))[1]])])]);
    }
    else if (s_2.indexOf("<") >= 0) {
        const p_3 = split(s_2, ["<"], undefined, 0);
        return new Lattice$1(2, [new Constraint(0, [undefined, new Bound$1(1, [((outArg_3 = (new Decimal("0")), [tryParse(item(1, p_3).trim(), new FSharpRef(() => outArg_3, (v_3) => {
            outArg_3 = v_3;
        })), outArg_3]))[1]])])]);
    }
    else if (((s_2.indexOf("LIKE") >= 0) ? true : (s_2.indexOf("~") >= 0)) ? true : (s_2.indexOf("SIMILAR TO") >= 0)) {
        return new Lattice$1(2, [new Constraint(9, ["Regex dummy"])]);
    }
    else {
        return new Lattice$1(0, []);
    }
}

export function transpile(sqlText) {
    try {
        const tables = parseSql(sqlText);
        const diagnostics = [];
        const enumerator = getEnumerator(tables);
        try {
            while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
                const t = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]();
                const enumerator_1 = getEnumerator(t.Columns);
                try {
                    while (enumerator_1["System.Collections.IEnumerator.MoveNext"]()) {
                        const c = enumerator_1["System.Collections.Generic.IEnumerator`1.get_Current"]();
                        if (length(c.CheckConstraints) > 0) {
                            if (equals(SemanticOptimizer_simplify(reduce((a, b) => (new Lattice$1(4, [a, b])), map(parseConstraintStr, c.CheckConstraints))), new Lattice$1(1, []))) {
                                void (diagnostics.push(`[DIAGNOSTIC CONTRADICTION] Table: ${t.Name}, Column: ${c.Name} has contradictory constraints that collapse to False.`));
                            }
                        }
                    }
                }
                finally {
                    disposeSafe(enumerator_1);
                }
            }
        }
        finally {
            disposeSafe(enumerator);
        }
        const tsSb = StringBuilder_$ctor();
        const ktSb = StringBuilder_$ctor();
        const swSb = StringBuilder_$ctor();
        const enumerator_2 = getEnumerator(tables);
        try {
            while (enumerator_2["System.Collections.IEnumerator.MoveNext"]()) {
                const t_1 = enumerator_2["System.Collections.Generic.IEnumerator`1.get_Current"]();
                const enumerator_3 = getEnumerator(t_1.Columns);
                try {
                    while (enumerator_3["System.Collections.IEnumerator.MoveNext"]()) {
                        const c_1 = enumerator_3["System.Collections.Generic.IEnumerator`1.get_Current"]();
                        if (!isEmpty(c_1.CheckConstraints)) {
                            const lattice_1 = reduce((a_1, b_1) => (new Lattice$1(4, [a_1, b_1])), map(parseConstraintStr, c_1.CheckConstraints));
                            const cNew = new ColumnDef(c_1.Name, c_1.DataType, c_1.IsNullable, c_1.IsPrimaryKey, c_1.DefaultValue, c_1.IsGenerated, c_1.Description, c_1.MaxLength, c_1.CheckConstraints, singleton(lattice_1), c_1.Semantics);
                            const patternInput = emitValidator(concat(t_1.Name, "_", c_1.Name), lattice_1, c_1.IsNullable, undefined);
                            const patternInput_1 = emitValidator_1(concat(t_1.Name, "_", c_1.Name), lattice_1, c_1.IsNullable, undefined);
                            const patternInput_2 = emitValidator_2(concat(t_1.Name, "_", c_1.Name), lattice_1, c_1.IsNullable, undefined);
                            StringBuilder__AppendLine_Z721C83C5(tsSb, patternInput[0]);
                            StringBuilder__AppendLine_Z721C83C5(ktSb, patternInput_1[0]);
                            StringBuilder__AppendLine_Z721C83C5(swSb, patternInput_2[0]);
                        }
                    }
                }
                finally {
                    disposeSafe(enumerator_3);
                }
            }
        }
        finally {
            disposeSafe(enumerator_2);
        }
        const fscheckCode = emitGenerators(map((t_2) => (new TableDef(t_2.Schema, t_2.Name, t_2.Type, t_2.Description, map((c_2) => {
            if (!isEmpty(c_2.CheckConstraints)) {
                return new ColumnDef(c_2.Name, c_2.DataType, c_2.IsNullable, c_2.IsPrimaryKey, c_2.DefaultValue, c_2.IsGenerated, c_2.Description, c_2.MaxLength, c_2.CheckConstraints, map(parseConstraintStr, c_2.CheckConstraints), c_2.Semantics);
            }
            else {
                return c_2;
            }
        }, t_2.Columns), t_2.PrimaryKeys, t_2.ForeignKeys, t_2.Indexes, t_2.TableConstraints)), tables));
        const typescript = toString(tsSb);
        const kotlin = toString(ktSb);
        const swift = toString(swSb);
        return {
            diagnostics: diagnostics.slice(),
            error: null,
            fscheck: fscheckCode,
            kotlin: kotlin,
            swift: swift,
            typescript: typescript,
        };
    }
    catch (ex) {
        return {
            diagnostics: [],
            error: ex.message,
            fscheck: "",
            kotlin: "",
            swift: "",
            typescript: "",
        };
    }
}

