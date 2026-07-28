
import { Divergence, FidelityModule_combine, Fidelity } from "../Canon.Core/Lineage.js";
import { replace, printf, toText, join, concat } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { map } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { isDigit } from "../fable_modules/fable-library-js.5.6.0/Char.js";
import { Lattice$1 } from "../Canon.Core/Lattice.js";

export function toSwift(predicate) {
    let clo, clo_1;
    switch (predicate.tag) {
        case 1:
            return ["false", new Fidelity(0, [])];
        case 3: {
            const patternInput = toSwift(predicate.fields[0]);
            return [concat("!(", patternInput[0], ")"), patternInput[1]];
        }
        case 4: {
            const patternInput_1 = toSwift(predicate.fields[0]);
            const patternInput_2 = toSwift(predicate.fields[1]);
            return [`(${patternInput_1[0]} && ${patternInput_2[0]})`, FidelityModule_combine(patternInput_1[1], patternInput_2[1])];
        }
        case 5: {
            const patternInput_3 = toSwift(predicate.fields[0]);
            const patternInput_4 = toSwift(predicate.fields[1]);
            return [`(${patternInput_3[0]} || ${patternInput_4[0]})`, FidelityModule_combine(patternInput_3[1], patternInput_4[1])];
        }
        case 2: {
            const c = predicate.fields[0];
            let matchResult, v, v_1, v_2, v_3, v_4, v_5, v_6, v_7, len, items, items_1, colA, colB, op, field, inner_1;
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
                    if (c.fields[0] == null) {
                        if (c.fields[1] != null) {
                            if (c.fields[1].tag === 0) {
                                matchResult = 11;
                                v_7 = c.fields[1].fields[0];
                            }
                            else {
                                matchResult = 9;
                                v_5 = c.fields[1].fields[0];
                            }
                        }
                        else {
                            matchResult = 12;
                        }
                    }
                    else if (c.fields[0].tag === 0) {
                        if (c.fields[1] == null) {
                            matchResult = 10;
                            v_6 = c.fields[0].fields[0];
                        }
                        else {
                            matchResult = 12;
                        }
                    }
                    else if (c.fields[1] == null) {
                        matchResult = 8;
                        v_4 = c.fields[0].fields[0];
                    }
                    else {
                        matchResult = 12;
                    }
                    break;
                }
                case 3: {
                    matchResult = 13;
                    len = c.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 14;
                    items = c.fields[0];
                    break;
                }
                case 5: {
                    matchResult = 15;
                    items_1 = c.fields[0];
                    break;
                }
                case 6: {
                    matchResult = 16;
                    colA = c.fields[0];
                    colB = c.fields[2];
                    op = c.fields[1];
                    break;
                }
                case 8: {
                    matchResult = 17;
                    break;
                }
                case 7: {
                    matchResult = 18;
                    break;
                }
                case 9: {
                    matchResult = 19;
                    break;
                }
                case 12: {
                    matchResult = 20;
                    field = c.fields[0];
                    inner_1 = c.fields[1];
                    break;
                }
                default:
                    matchResult = 0;
            }
            switch (matchResult) {
                case 0:
                    return ["value == nil", new Fidelity(0, [])];
                case 1:
                    return ["value != nil", new Fidelity(0, [])];
                case 2:
                    return [`value > Decimal(string: "${v}")!`, new Fidelity(0, [])];
                case 3:
                    return [`value < Decimal(string: "${v_1}")!`, new Fidelity(0, [])];
                case 4:
                    return [`value >= Decimal(string: "${v_2}")!`, new Fidelity(0, [])];
                case 5:
                    return [`value <= Decimal(string: "${v_3}")!`, new Fidelity(0, [])];
                case 6:
                    return ["true", new Fidelity(2, [new Divergence(1, ["Complex range bounds not fully implemented in Swift"])])];
                case 7:
                    return ["value.isSignalingNaN == false", new Fidelity(2, [new Divergence(1, ["Int range check"])])];
                case 8:
                    return [concat("value > \"", v_4, "\""), new Fidelity(2, [new Divergence(1, ["String range collation may differ"])])];
                case 9:
                    return [concat("value < \"", v_5, "\""), new Fidelity(2, [new Divergence(1, ["String range collation may differ"])])];
                case 10:
                    return [concat("value >= \"", v_6, "\""), new Fidelity(2, [new Divergence(1, ["String range collation may differ"])])];
                case 11:
                    return [concat("value <= \"", v_7, "\""), new Fidelity(2, [new Divergence(1, ["String range collation may differ"])])];
                case 12:
                    return ["true", new Fidelity(2, [new Divergence(1, ["Complex string range bounds not fully implemented in Swift"])])];
                case 13:
                    return [`value.count <= ${len}`, new Fidelity(0, [])];
                case 14:
                    return [concat("[", join(", ", map((clo = toText(printf("\"%s\"")), clo), items)), "].contains(value)"), new Fidelity(0, [])];
                case 15:
                    return [concat("Set([", join(", ", map((clo_1 = toText(printf("\"%s\"")), clo_1), items_1)), "]).contains(value)"), new Fidelity(0, [])];
                case 16: {
                    const isLiteral = (s) => {
                        if (s.length > 0) {
                            if (((s[0] === "\"") ? true : (s[0] === "\'")) ? true : isDigit(s[0])) {
                                return true;
                            }
                            else {
                                return s[0] === "-";
                            }
                        }
                        else {
                            return false;
                        }
                    };
                    return [`${isLiteral(colA) ? colA : concat("value.", colA)} ${op} ${isLiteral(colB) ? colB : concat("value.", colB)}`, new Fidelity(0, [])];
                }
                case 17:
                    return ["true", new Fidelity(5, ["PrimaryKey concept does not exist in Swift validators"])];
                case 18:
                    return ["!value.isEmpty", new Fidelity(0, [])];
                case 19:
                    return ["true", new Fidelity(5, ["Cannot transpile raw SQL"])];
                default: {
                    const patternInput_5 = toSwift(new Lattice$1(2, [inner_1]));
                    return [replace(patternInput_5[0], "value", concat("value.", field)), patternInput_5[1]];
                }
            }
        }
        default:
            return ["true", new Fidelity(0, [])];
    }
}

/**
 * Emits a full Swift validation function and its Fidelity grade.
 */
export function emitValidator(name, predicate, isNullable, provenance) {
    const patternInput = toSwift(predicate);
    const guard = isNullable ? "\n    if value == nil { return true }" : "";
    return [`${(provenance == null) ? "" : concat("// Provenance: ", provenance, "\n")}func validate_${name}(value: Any?) -> Bool {${guard}
    return ${patternInput[0]}
}`, patternInput[1]];
}

