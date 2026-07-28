
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, string_type, union_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { printf, toText, join, concat } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { choose } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { equals } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { Lattice$1, SemanticOptimizer_simplify } from "./Lattice.js";

/**
 * Represents the severity of constraint drift between the database and a target system.
 */
export class DriftSeverity extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Low", "Medium", "High"];
    }
}

export function DriftSeverity_$reflection() {
    return union_type("Canon.Core.DriftSeverity", [], DriftSeverity, () => [[], [], []]);
}

/**
 * A report detailing a specific drift violation.
 */
export class DriftViolation extends Record {
    constructor(Field, TargetSystem, DatabaseTruth, TargetReality, Severity, FixAction) {
        super();
        this.Field = Field;
        this.TargetSystem = TargetSystem;
        this.DatabaseTruth = DatabaseTruth;
        this.TargetReality = TargetReality;
        this.Severity = Severity;
        this.FixAction = FixAction;
    }
}

export function DriftViolation_$reflection() {
    return record_type("Canon.Core.DriftViolation", [], DriftViolation, () => [["Field", string_type], ["TargetSystem", string_type], ["DatabaseTruth", string_type], ["TargetReality", string_type], ["Severity", DriftSeverity_$reflection()], ["FixAction", string_type]]);
}

export function DriftEngine_analyzeFidelity(field, targetSystem, fidelity, dbConstraintStr) {
    let matchResult, reason, reason_1, a, reason_2;
    switch (fidelity.tag) {
        case 3:
        case 4: {
            matchResult = 4;
            break;
        }
        case 2: {
            if (fidelity.fields[0].tag === 1) {
                matchResult = 2;
                reason_1 = fidelity.fields[0].fields[0];
            }
            else {
                matchResult = 1;
                reason = fidelity.fields[0].fields[0];
            }
            break;
        }
        case 1: {
            matchResult = 3;
            a = fidelity.fields[0];
            break;
        }
        case 5: {
            matchResult = 5;
            reason_2 = fidelity.fields[0];
            break;
        }
        default:
            matchResult = 0;
    }
    switch (matchResult) {
        case 0:
            return undefined;
        case 1:
            return new DriftViolation(field, targetSystem, dbConstraintStr, "Approximate (Stronger)", new DriftSeverity(1, []), concat("Review frontend validator bounds. Reason: ", reason));
        case 2:
            return new DriftViolation(field, targetSystem, dbConstraintStr, "Approximate (Weaker)", new DriftSeverity(2, []), concat("Target admits invalid writes. Reason: ", reason_1));
        case 3: {
            const str = join(", ", a);
            return new DriftViolation(field, targetSystem, dbConstraintStr, "Conditional", new DriftSeverity(0, []), toText(printf("Ensure runtime assumptions hold: %s"))(str));
        }
        case 4:
            return undefined;
        default:
            return new DriftViolation(field, targetSystem, dbConstraintStr, "Missing / Unsupported", new DriftSeverity(2, []), concat("Implement custom backend middleware guard. Reason: ", reason_2));
    }
}

/**
 * Generates a full drift report for a table based on generated fidelities.
 */
export function DriftEngine_detectDrift(table, fidelities) {
    return choose((tupledArg) => DriftEngine_analyzeFidelity(tupledArg[0], tupledArg[1], tupledArg[2], tupledArg[3]), fidelities);
}

export class DriftEngine_SemanticDriftStatus extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Aligned", "StrictTarget", "LooseTarget", "Disjoint", "Unknown"];
    }
}

export function DriftEngine_SemanticDriftStatus_$reflection() {
    return union_type("Canon.Core.DriftEngine.SemanticDriftStatus", [], DriftEngine_SemanticDriftStatus, () => [[], [], [], [], []]);
}

function DriftEngine_structuralEquals(a_mut, b_mut) {
    DriftEngine_structuralEquals:
    while (true) {
        const a = a_mut, b = b_mut;
        let matchResult, c1, c2, x, y, a1, a2, b1, b2, a1_1, a2_1, b1_1, b2_1;
        switch (a.tag) {
            case 1: {
                if (b.tag === 1) {
                    matchResult = 1;
                }
                else {
                    matchResult = 6;
                }
                break;
            }
            case 2: {
                if (b.tag === 2) {
                    matchResult = 2;
                    c1 = a.fields[0];
                    c2 = b.fields[0];
                }
                else {
                    matchResult = 6;
                }
                break;
            }
            case 3: {
                if (b.tag === 3) {
                    matchResult = 3;
                    x = a.fields[0];
                    y = b.fields[0];
                }
                else {
                    matchResult = 6;
                }
                break;
            }
            case 4: {
                if (b.tag === 4) {
                    matchResult = 4;
                    a1 = a.fields[0];
                    a2 = b.fields[0];
                    b1 = a.fields[1];
                    b2 = b.fields[1];
                }
                else {
                    matchResult = 6;
                }
                break;
            }
            case 5: {
                if (b.tag === 5) {
                    matchResult = 5;
                    a1_1 = a.fields[0];
                    a2_1 = b.fields[0];
                    b1_1 = a.fields[1];
                    b2_1 = b.fields[1];
                }
                else {
                    matchResult = 6;
                }
                break;
            }
            default:
                if (b.tag === 0) {
                    matchResult = 0;
                }
                else {
                    matchResult = 6;
                }
        }
        switch (matchResult) {
            case 0:
                return true;
            case 1:
                return true;
            case 2:
                return equals(c1, c2);
            case 3: {
                a_mut = x;
                b_mut = y;
                continue DriftEngine_structuralEquals;
            }
            case 4:
                if (DriftEngine_structuralEquals(a1, a2) && DriftEngine_structuralEquals(b1, b2)) {
                    return true;
                }
                else if (DriftEngine_structuralEquals(a1, b2)) {
                    a_mut = b1;
                    b_mut = a2;
                    continue DriftEngine_structuralEquals;
                }
                else {
                    return false;
                }
            case 5:
                if (DriftEngine_structuralEquals(a1_1, a2_1) && DriftEngine_structuralEquals(b1_1, b2_1)) {
                    return true;
                }
                else if (DriftEngine_structuralEquals(a1_1, b2_1)) {
                    a_mut = b1_1;
                    b_mut = a2_1;
                    continue DriftEngine_structuralEquals;
                }
                else {
                    return false;
                }
            default:
                return false;
        }
        break;
    }
}

/**
 * Calculates the semantic drift status of a target constraint relative to a source constraint.
 */
export function DriftEngine_calculateSemanticDrift(source, target) {
    const optSource = SemanticOptimizer_simplify(source);
    const optTarget = SemanticOptimizer_simplify(target);
    const intersection = SemanticOptimizer_simplify(new Lattice$1(4, [optSource, optTarget]));
    if (intersection.tag === 1) {
        return new DriftEngine_SemanticDriftStatus(3, []);
    }
    else if (DriftEngine_structuralEquals(optSource, optTarget)) {
        return new DriftEngine_SemanticDriftStatus(0, []);
    }
    else if (DriftEngine_structuralEquals(intersection, optTarget)) {
        return new DriftEngine_SemanticDriftStatus(1, []);
    }
    else if (DriftEngine_structuralEquals(intersection, optSource)) {
        return new DriftEngine_SemanticDriftStatus(2, []);
    }
    else {
        return new DriftEngine_SemanticDriftStatus(4, []);
    }
}

