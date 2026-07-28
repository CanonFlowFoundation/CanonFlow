
import { FidelityModule_combine, Divergence, Fidelity } from "../Canon.Core/Lineage.js";
import { printf, toText, join, concat } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { length, map } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { isDigit } from "../fable_modules/fable-library-js.5.6.0/Char.js";
import { Lattice$1 } from "../Canon.Core/Lattice.js";

export function toTypeScript(predicate) {
    let clo, clo_1;
    switch (predicate.tag) {
        case 1:
            return ["z.never()", new Fidelity(0, [])];
        case 3:
            return [concat("z.any().refine(val => !(", toTypeScript(predicate.fields[0])[0], ".safeParse(val).success))"), new Fidelity(2, [new Divergence(1, ["Zod does not support generic NOT"])])];
        case 4: {
            const patternInput_1 = toTypeScript(predicate.fields[0]);
            const patternInput_2 = toTypeScript(predicate.fields[1]);
            return [concat(patternInput_1[0], ".and(", patternInput_2[0], ")"), FidelityModule_combine(patternInput_1[1], patternInput_2[1])];
        }
        case 5: {
            const patternInput_3 = toTypeScript(predicate.fields[0]);
            const patternInput_4 = toTypeScript(predicate.fields[1]);
            return [concat(patternInput_3[0], ".or(", patternInput_4[0], ")"), FidelityModule_combine(patternInput_3[1], patternInput_4[1])];
        }
        case 2: {
            const c = predicate.fields[0];
            let matchResult, v, v_1, v_2, v_3, len, items, items_1, colA, colB, op, field, inner_1;
            switch (c.tag) {
                case 11: {
                    matchResult = 1;
                    break;
                }
                case 0: {
                    if (c.fields[0] == null) {
                        if (c.fields[1] != null) {
                            if (c.fields[1].tag === 0) {
                                matchResult = 5;
                                v_3 = c.fields[1].fields[0];
                            }
                            else {
                                matchResult = 3;
                                v_1 = c.fields[1].fields[0];
                            }
                        }
                        else {
                            matchResult = 6;
                        }
                    }
                    else if (c.fields[0].tag === 0) {
                        if (c.fields[1] == null) {
                            matchResult = 4;
                            v_2 = c.fields[0].fields[0];
                        }
                        else {
                            matchResult = 6;
                        }
                    }
                    else if (c.fields[1] == null) {
                        matchResult = 2;
                        v = c.fields[0].fields[0];
                    }
                    else {
                        matchResult = 6;
                    }
                    break;
                }
                case 1: {
                    matchResult = 7;
                    break;
                }
                case 2: {
                    matchResult = 8;
                    break;
                }
                case 3: {
                    matchResult = 9;
                    len = c.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 10;
                    items = c.fields[0];
                    break;
                }
                case 5: {
                    matchResult = 11;
                    items_1 = c.fields[0];
                    break;
                }
                case 6: {
                    matchResult = 12;
                    colA = c.fields[0];
                    colB = c.fields[2];
                    op = c.fields[1];
                    break;
                }
                case 8: {
                    matchResult = 13;
                    break;
                }
                case 7: {
                    matchResult = 14;
                    break;
                }
                case 9: {
                    matchResult = 15;
                    break;
                }
                case 12: {
                    matchResult = 16;
                    field = c.fields[0];
                    inner_1 = c.fields[1];
                    break;
                }
                default:
                    matchResult = 0;
            }
            switch (matchResult) {
                case 0:
                    return ["z.null()", new Fidelity(0, [])];
                case 1:
                    return ["z.any().refine(val => val !== null && val !== undefined)", new Fidelity(0, [])];
                case 2:
                    return [`z.number().gt(${v})`, new Fidelity(0, [])];
                case 3:
                    return [`z.number().lt(${v_1})`, new Fidelity(0, [])];
                case 4:
                    return [`z.number().gte(${v_2})`, new Fidelity(0, [])];
                case 5:
                    return [`z.number().lte(${v_3})`, new Fidelity(0, [])];
                case 6:
                    return ["z.number()", new Fidelity(2, [new Divergence(1, ["Complex range bounds not fully implemented in TS Zod"])])];
                case 7:
                    return ["z.number().int()", new Fidelity(2, [new Divergence(1, ["Int range requires precision bounds"])])];
                case 8:
                    return ["z.string()", new Fidelity(2, [new Divergence(1, ["String range collation may differ"])])];
                case 9:
                    return [`z.string().max(${len})`, new Fidelity(0, [])];
                case 10: {
                    const arr = join(", ", map((clo = toText(printf("\"%s\"")), clo), items));
                    if (length(items) === 0) {
                        return ["z.never()", new Fidelity(0, [])];
                    }
                    else {
                        return [concat("z.enum([", arr, "])"), new Fidelity(0, [])];
                    }
                }
                case 11: {
                    const arr_1 = join(", ", map((clo_1 = toText(printf("\"%s\"")), clo_1), items_1));
                    if (length(items_1) === 0) {
                        return ["z.never()", new Fidelity(0, [])];
                    }
                    else {
                        return [concat("z.enum([", arr_1, "])"), new Fidelity(0, [])];
                    }
                }
                case 12: {
                    const isLiteral = (s) => {
                        if (s.startsWith("\'") ? true : isDigit(s[0])) {
                            return true;
                        }
                        else {
                            return s.startsWith("-");
                        }
                    };
                    return [`z.any().refine(data => ${isLiteral(colA) ? colA : concat("data.", colA)} ${op} ${isLiteral(colB) ? colB : concat("data.", colB)})`, new Fidelity(0, [])];
                }
                case 13:
                    return ["z.any()", new Fidelity(5, ["PrimaryKey concept does not exist in TS validators"])];
                case 14:
                    return ["z.string().min(1)", new Fidelity(0, [])];
                case 15:
                    return ["z.any()", new Fidelity(5, ["Cannot transpile raw SQL"])];
                default: {
                    const patternInput_5 = toTypeScript(new Lattice$1(2, [inner_1]));
                    return [`z.object({ ${field}: ${patternInput_5[0]} })`, patternInput_5[1]];
                }
            }
        }
        default:
            return ["z.any()", new Fidelity(0, [])];
    }
}

/**
 * Emits a full TypeScript validation function and its Fidelity grade.
 */
export function emitValidator(name, predicate, isNullable, provenance) {
    const patternInput = toTypeScript(predicate);
    const expr = patternInput[0];
    const baseCode = isNullable ? concat(expr, ".nullable()") : expr;
    return [`import { z } from "zod";

${(provenance == null) ? "" : concat("// Provenance: ", provenance, "\n")}export const ${name}Schema = ${baseCode};

export function validate_${name}(value: any): boolean {
    return ${name}Schema.safeParse(value).success;
}`, patternInput[1]];
}

