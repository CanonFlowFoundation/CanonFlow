
import { sanitizeComment } from "./Sanitizer.js";
import { concat, join, printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { float64_type, bool_type, record_type, list_type, union_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Lattice$1_$reflection, Constraint_$reflection } from "./Lattice.js";
import { append } from "../fable_modules/fable-library-js.5.6.0/List.js";

/**
 * Direction matters. These have different owners and different blast radii.
 */
export class Divergence extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Stronger", "Weaker"];
    }
    toString() {
        const this$ = this;
        if (this$.tag === 1) {
            const arg_1 = sanitizeComment(this$.fields[0]);
            return toText(printf("Weaker: %s"))(arg_1);
        }
        else {
            const arg = sanitizeComment(this$.fields[0]);
            return toText(printf("Stronger: %s"))(arg);
        }
    }
}

export function Divergence_$reflection() {
    return union_type("Canon.Core.Divergence", [], Divergence, () => [[["reason", string_type]], [["reason", string_type]]]);
}

/**
 * Represents the translation fidelity of a constraint into a target language.
 */
export class Fidelity extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Exact", "Conditional", "Approximate", "DatabaseOwned", "Manual", "Unsupported"];
    }
    toString() {
        const this$ = this;
        switch (this$.tag) {
            case 1: {
                const str = join(", ", this$.fields[0]);
                return toText(printf("Conditional: [%s]"))(str);
            }
            case 2:
                return toText(printf("Approximate (%O)"))(this$.fields[0]);
            case 3:
                return toText(printf("DatabaseOwned by %s"))(this$.fields[0]);
            case 4:
                return toText(printf("Manual (%s, %s)"))(this$.fields[0])(this$.fields[1]);
            case 5: {
                const arg_5 = sanitizeComment(this$.fields[0]);
                return toText(printf("Unsupported: %s"))(arg_5);
            }
            default:
                return "Exact";
        }
    }
}

export function Fidelity_$reflection() {
    return union_type("Canon.Core.Fidelity", [], Fidelity, () => [[], [["assumptions", list_type(string_type)]], [["Item", Divergence_$reflection()]], [["enforcer", string_type]], [["owner", string_type], ["evidenceRef", string_type]], [["reason", string_type]]]);
}

export class ConstraintFidelity extends Record {
    constructor(Constraint, Fidelity, Target) {
        super();
        this.Constraint = Constraint;
        this.Fidelity = Fidelity;
        this.Target = Target;
    }
}

export function ConstraintFidelity_$reflection() {
    return record_type("Canon.Core.ConstraintFidelity", [], ConstraintFidelity, () => [["Constraint", Lattice$1_$reflection(Constraint_$reflection())], ["Fidelity", Fidelity_$reflection()], ["Target", string_type]]);
}

export function FidelityModule_combine(f1, f2) {
    let matchResult, r1, r2, r, d1, d2, d, e, o, e1, a1, a2, a;
    switch (f1.tag) {
        case 2: {
            switch (f2.tag) {
                case 5: {
                    matchResult = 1;
                    r = f2.fields[0];
                    break;
                }
                case 2: {
                    matchResult = 2;
                    d1 = f1.fields[0];
                    d2 = f2.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 3;
                    d = f1.fields[0];
                    break;
                }
                case 3: {
                    matchResult = 3;
                    d = f1.fields[0];
                    break;
                }
                case 1: {
                    matchResult = 3;
                    d = f1.fields[0];
                    break;
                }
                default: {
                    matchResult = 3;
                    d = f1.fields[0];
                }
            }
            break;
        }
        case 4: {
            switch (f2.tag) {
                case 5: {
                    matchResult = 1;
                    r = f2.fields[0];
                    break;
                }
                case 2: {
                    matchResult = 3;
                    d = f2.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 4;
                    e = f1.fields[1];
                    o = f1.fields[0];
                    break;
                }
                case 3: {
                    matchResult = 4;
                    e = f1.fields[1];
                    o = f1.fields[0];
                    break;
                }
                case 1: {
                    matchResult = 4;
                    e = f1.fields[1];
                    o = f1.fields[0];
                    break;
                }
                default: {
                    matchResult = 4;
                    e = f1.fields[1];
                    o = f1.fields[0];
                }
            }
            break;
        }
        case 3: {
            switch (f2.tag) {
                case 5: {
                    matchResult = 1;
                    r = f2.fields[0];
                    break;
                }
                case 2: {
                    matchResult = 3;
                    d = f2.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 4;
                    e = f2.fields[1];
                    o = f2.fields[0];
                    break;
                }
                case 3: {
                    matchResult = 5;
                    e1 = f1.fields[0];
                    break;
                }
                case 1: {
                    matchResult = 5;
                    e1 = f1.fields[0];
                    break;
                }
                default: {
                    matchResult = 5;
                    e1 = f1.fields[0];
                }
            }
            break;
        }
        case 1: {
            switch (f2.tag) {
                case 5: {
                    matchResult = 1;
                    r = f2.fields[0];
                    break;
                }
                case 2: {
                    matchResult = 3;
                    d = f2.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 4;
                    e = f2.fields[1];
                    o = f2.fields[0];
                    break;
                }
                case 3: {
                    matchResult = 5;
                    e1 = f2.fields[0];
                    break;
                }
                case 1: {
                    matchResult = 6;
                    a1 = f1.fields[0];
                    a2 = f2.fields[0];
                    break;
                }
                default: {
                    matchResult = 7;
                    a = f1.fields[0];
                }
            }
            break;
        }
        case 0: {
            switch (f2.tag) {
                case 2: {
                    matchResult = 3;
                    d = f2.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 4;
                    e = f2.fields[1];
                    o = f2.fields[0];
                    break;
                }
                case 3: {
                    matchResult = 5;
                    e1 = f2.fields[0];
                    break;
                }
                case 1: {
                    matchResult = 7;
                    a = f2.fields[0];
                    break;
                }
                case 0: {
                    matchResult = 8;
                    break;
                }
                default: {
                    matchResult = 1;
                    r = f2.fields[0];
                }
            }
            break;
        }
        default:
            switch (f2.tag) {
                case 5: {
                    matchResult = 0;
                    r1 = f1.fields[0];
                    r2 = f2.fields[0];
                    break;
                }
                case 2: {
                    matchResult = 1;
                    r = f1.fields[0];
                    break;
                }
                case 4: {
                    matchResult = 1;
                    r = f1.fields[0];
                    break;
                }
                case 3: {
                    matchResult = 1;
                    r = f1.fields[0];
                    break;
                }
                case 1: {
                    matchResult = 1;
                    r = f1.fields[0];
                    break;
                }
                default: {
                    matchResult = 1;
                    r = f1.fields[0];
                }
            }
    }
    switch (matchResult) {
        case 0:
            return new Fidelity(5, [concat(r1, "; ", r2)]);
        case 1:
            return new Fidelity(5, [r]);
        case 2:
            return new Fidelity(2, [new Divergence(1, [`${d1} and ${d2}`])]);
        case 3:
            return new Fidelity(2, [d]);
        case 4:
            return new Fidelity(4, [o, e]);
        case 5:
            return new Fidelity(3, [e1]);
        case 6:
            return new Fidelity(1, [append(a1, a2)]);
        case 7:
            return new Fidelity(1, [a]);
        default:
            return new Fidelity(0, []);
    }
}

export class FidelityReport extends Record {
    constructor(Schema, Passed, Score, LostMeaning) {
        super();
        this.Schema = Schema;
        this.Passed = Passed;
        this.Score = Score;
        this.LostMeaning = LostMeaning;
    }
}

export function FidelityReport_$reflection() {
    return record_type("Canon.Core.FidelityReport", [], FidelityReport, () => [["Schema", string_type], ["Passed", bool_type], ["Score", float64_type], ["LostMeaning", list_type(string_type)]]);
}

/**
 * Lineage grade indicates the degree of trust/verification for a field or constraint.
 * Inspired by Symphony's Lineage concepts.
 */
export class LineageGrade extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Exact", "Declared", "Opaque"];
    }
}

export function LineageGrade_$reflection() {
    return union_type("Canon.Core.LineageGrade", [], LineageGrade, () => [[], [], []]);
}

