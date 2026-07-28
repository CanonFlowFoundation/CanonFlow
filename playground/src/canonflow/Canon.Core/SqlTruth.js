
import { Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { union_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { value } from "../fable_modules/fable-library-js.5.6.0/Option.js";

/**
 * PostgreSQL truth. Never collapsed to bool before the acceptance test.
 */
export class SqlTruth extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["True", "Unknown", "False"];
    }
}

export function SqlTruth_$reflection() {
    return union_type("Canon.Core.SqlTruth", [], SqlTruth, () => [[], [], []]);
}

export function SqlTruthModule_negate(_arg) {
    switch (_arg.tag) {
        case 2:
            return new SqlTruth(0, []);
        case 1:
            return new SqlTruth(1, []);
        default:
            return new SqlTruth(2, []);
    }
}

export function SqlTruthModule_conj(a, b) {
    let matchResult;
    switch (a.tag) {
        case 1: {
            switch (b.tag) {
                case 2: {
                    matchResult = 0;
                    break;
                }
                default:
                    matchResult = 1;
            }
            break;
        }
        case 0: {
            switch (b.tag) {
                case 1: {
                    matchResult = 1;
                    break;
                }
                case 0: {
                    matchResult = 2;
                    break;
                }
                default:
                    matchResult = 0;
            }
            break;
        }
        default:
            matchResult = 0;
    }
    switch (matchResult) {
        case 0:
            return new SqlTruth(2, []);
        case 1:
            return new SqlTruth(1, []);
        default:
            return new SqlTruth(0, []);
    }
}

export function SqlTruthModule_disj(a, b) {
    let matchResult;
    switch (a.tag) {
        case 1: {
            switch (b.tag) {
                case 0: {
                    matchResult = 0;
                    break;
                }
                default:
                    matchResult = 1;
            }
            break;
        }
        case 2: {
            switch (b.tag) {
                case 1: {
                    matchResult = 1;
                    break;
                }
                case 2: {
                    matchResult = 2;
                    break;
                }
                default:
                    matchResult = 0;
            }
            break;
        }
        default:
            matchResult = 0;
    }
    switch (matchResult) {
        case 0:
            return new SqlTruth(0, []);
        case 1:
            return new SqlTruth(1, []);
        default:
            return new SqlTruth(2, []);
    }
}

/**
 * Law 3. The ONLY SqlTruth -> bool in the codebase.
 */
export function SqlTruthModule_admits(_arg) {
    switch (_arg.tag) {
        case 2:
            return false;
        default:
            return true;
    }
}

/**
 * Comparisons propagate Unknown; IS NULL never does.
 */
export function SqlTruthModule_compare3(f, l, r) {
    let matchResult, a, b;
    if (l != null) {
        if (r != null) {
            matchResult = 0;
            a = value(l);
            b = value(r);
        }
        else {
            matchResult = 1;
        }
    }
    else {
        matchResult = 1;
    }
    switch (matchResult) {
        case 0:
            if (f(a, b)) {
                return new SqlTruth(0, []);
            }
            else {
                return new SqlTruth(2, []);
            }
        default:
            return new SqlTruth(1, []);
    }
}

export function SqlTruthModule_isNull(v) {
    if (v == null) {
        return new SqlTruth(0, []);
    }
    else {
        return new SqlTruth(2, []);
    }
}

