
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, list_type, int32_type, string_type, int64_type, option_type, decimal_type, union_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { equals, compare } from "../fable_modules/fable-library-js.5.6.0/Decimal.js";
import { equals as equals_1 } from "../fable_modules/fable-library-js.5.6.0/Util.js";

/**
 * Three-valued logic for autonomous evaluation
 */
export class LatticeOutcome extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Satisfied", "Violated", "Unknown"];
    }
}

export function LatticeOutcome_$reflection() {
    return union_type("Canon.Core.LatticeOutcome", [], LatticeOutcome, () => [[], [], []]);
}

export function LatticeOutcome__Not(this$) {
    switch (this$.tag) {
        case 1:
            return new LatticeOutcome(0, []);
        case 2:
            return new LatticeOutcome(2, []);
        default:
            return new LatticeOutcome(1, []);
    }
}

export function LatticeOutcome__And_Z16C1C285(this$, other) {
    let matchResult;
    switch (this$.tag) {
        case 0: {
            switch (other.tag) {
                case 0: {
                    matchResult = 0;
                    break;
                }
                case 1: {
                    matchResult = 1;
                    break;
                }
                default:
                    matchResult = 2;
            }
            break;
        }
        case 1: {
            matchResult = 1;
            break;
        }
        default:
            if (other.tag === 1) {
                matchResult = 1;
            }
            else {
                matchResult = 2;
            }
    }
    switch (matchResult) {
        case 0:
            return new LatticeOutcome(0, []);
        case 1:
            return new LatticeOutcome(1, []);
        default:
            return new LatticeOutcome(2, []);
    }
}

export function LatticeOutcome__Or_Z16C1C285(this$, other) {
    let matchResult;
    switch (this$.tag) {
        case 1: {
            switch (other.tag) {
                case 1: {
                    matchResult = 0;
                    break;
                }
                case 0: {
                    matchResult = 1;
                    break;
                }
                default:
                    matchResult = 2;
            }
            break;
        }
        case 0: {
            matchResult = 1;
            break;
        }
        default:
            if (other.tag === 0) {
                matchResult = 1;
            }
            else {
                matchResult = 2;
            }
    }
    switch (matchResult) {
        case 0:
            return new LatticeOutcome(1, []);
        case 1:
            return new LatticeOutcome(0, []);
        default:
            return new LatticeOutcome(2, []);
    }
}

/**
 * Represents Helios capability-typed field kinds for semantic indexing.
 */
export class FieldKind extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Keyword", "Text", "TextWithKeyword", "Date", "Numeric", "Bool", "Nested"];
    }
}

export function FieldKind_$reflection() {
    return union_type("Canon.Core.FieldKind", [], FieldKind, () => [[], [], [], [], [], [], []]);
}

/**
 * Represents boundary definitions for Symphony constraints.
 */
export class Bound$1 extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Inclusive", "Exclusive"];
    }
}

export function Bound$1_$reflection(gen0) {
    return union_type("Canon.Core.Bound`1", [gen0], Bound$1, () => [[["Item", gen0]], [["Item", gen0]]]);
}

/**
 * Defines common validation constraints that can be used as Leaves in our Lattice (Symphony model).
 */
export class Constraint extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Range", "IntRange", "StringRange", "MaxLength", "InList", "InSet", "RelativeBound", "NonEmpty", "PrimaryKey", "Opaque", "IsNull", "IsNotNull", "FieldBound"];
    }
}

export function Constraint_$reflection() {
    return union_type("Canon.Core.Constraint", [], Constraint, () => [[["lo", option_type(Bound$1_$reflection(decimal_type))], ["hi", option_type(Bound$1_$reflection(decimal_type))]], [["lo", option_type(Bound$1_$reflection(int64_type))], ["hi", option_type(Bound$1_$reflection(int64_type))]], [["lo", option_type(Bound$1_$reflection(string_type))], ["hi", option_type(Bound$1_$reflection(string_type))]], [["Item", int32_type]], [["Item", list_type(string_type)]], [["Item", list_type(string_type)]], [["colA", string_type], ["op", string_type], ["colB", string_type]], [], [], [["Item", string_type]], [], [], [["Item1", string_type], ["Item2", Constraint_$reflection()]]]);
}

/**
 * A closed six-constructor bounded complemented lattice for query formulation.
 */
export class Lattice$1 extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["True", "False", "Leaf", "Not", "And", "Or"];
    }
}

export function Lattice$1_$reflection(gen0) {
    return union_type("Canon.Core.Lattice`1", [gen0], Lattice$1, () => [[], [], [["Item", gen0]], [["Item", Lattice$1_$reflection(gen0)]], [["Item1", Lattice$1_$reflection(gen0)], ["Item2", Lattice$1_$reflection(gen0)]], [["Item1", Lattice$1_$reflection(gen0)], ["Item2", Lattice$1_$reflection(gen0)]]]);
}

export function Lattice_not(l) {
    switch (l.tag) {
        case 0:
            return new Lattice$1(1, []);
        case 1:
            return new Lattice$1(0, []);
        case 3:
            return l.fields[0];
        default:
            return new Lattice$1(3, [l]);
    }
}

export function Lattice_and$0027(left, right) {
    let matchResult, x;
    switch (left.tag) {
        case 0: {
            matchResult = 0;
            x = right;
            break;
        }
        case 1: {
            switch (right.tag) {
                case 0: {
                    matchResult = 0;
                    x = left;
                    break;
                }
                default:
                    matchResult = 1;
            }
            break;
        }
        default:
            switch (right.tag) {
                case 0: {
                    matchResult = 0;
                    x = left;
                    break;
                }
                case 1: {
                    matchResult = 1;
                    break;
                }
                default:
                    matchResult = 2;
            }
    }
    switch (matchResult) {
        case 0:
            return x;
        case 1:
            return new Lattice$1(1, []);
        default:
            return new Lattice$1(4, [left, right]);
    }
}

export function Lattice_or$0027(left, right) {
    let matchResult, x;
    switch (left.tag) {
        case 0: {
            matchResult = 0;
            break;
        }
        case 1: {
            switch (right.tag) {
                case 0: {
                    matchResult = 0;
                    break;
                }
                case 1: {
                    matchResult = 1;
                    x = right;
                    break;
                }
                default: {
                    matchResult = 1;
                    x = right;
                }
            }
            break;
        }
        default:
            switch (right.tag) {
                case 0: {
                    matchResult = 0;
                    break;
                }
                case 1: {
                    matchResult = 1;
                    x = left;
                    break;
                }
                default:
                    matchResult = 2;
            }
    }
    switch (matchResult) {
        case 0:
            return new Lattice$1(0, []);
        case 1:
            return x;
        default:
            return new Lattice$1(5, [left, right]);
    }
}

export function Lattice_toNNF(l_mut) {
    Lattice_toNNF:
    while (true) {
        const l = l_mut;
        switch (l.tag) {
            case 1:
                return new Lattice$1(1, []);
            case 2:
                return new Lattice$1(2, [l.fields[0]]);
            case 4:
                return new Lattice$1(4, [Lattice_toNNF(l.fields[0]), Lattice_toNNF(l.fields[1])]);
            case 5:
                return new Lattice$1(5, [Lattice_toNNF(l.fields[0]), Lattice_toNNF(l.fields[1])]);
            case 3: {
                const inner = l.fields[0];
                switch (inner.tag) {
                    case 1:
                        return new Lattice$1(0, []);
                    case 2:
                        return new Lattice$1(3, [new Lattice$1(2, [inner.fields[0]])]);
                    case 3: {
                        l_mut = inner.fields[0];
                        continue Lattice_toNNF;
                    }
                    case 4:
                        return new Lattice$1(5, [Lattice_toNNF(new Lattice$1(3, [inner.fields[0]])), Lattice_toNNF(new Lattice$1(3, [inner.fields[1]]))]);
                    case 5:
                        return new Lattice$1(4, [Lattice_toNNF(new Lattice$1(3, [inner.fields[0]])), Lattice_toNNF(new Lattice$1(3, [inner.fields[1]]))]);
                    default:
                        return new Lattice$1(1, []);
                }
            }
            default:
                return new Lattice$1(0, []);
        }
        break;
    }
}

export function Lattice_eval3(evalLeaf, l) {
    switch (l.tag) {
        case 1:
            return new LatticeOutcome(1, []);
        case 2:
            return evalLeaf(l.fields[0]);
        case 3:
            return LatticeOutcome__Not(Lattice_eval3(evalLeaf, l.fields[0]));
        case 4:
            return LatticeOutcome__And_Z16C1C285(Lattice_eval3(evalLeaf, l.fields[0]), Lattice_eval3(evalLeaf, l.fields[1]));
        case 5:
            return LatticeOutcome__Or_Z16C1C285(Lattice_eval3(evalLeaf, l.fields[0]), Lattice_eval3(evalLeaf, l.fields[1]));
        default:
            return new LatticeOutcome(0, []);
    }
}

export function Lattice_eval(evalLeaf_mut, l_mut) {
    Lattice_eval:
    while (true) {
        const evalLeaf = evalLeaf_mut, l = l_mut;
        switch (l.tag) {
            case 1:
                return false;
            case 2:
                return evalLeaf(l.fields[0]);
            case 3:
                return !Lattice_eval(evalLeaf, l.fields[0]);
            case 4:
                if (Lattice_eval(evalLeaf, l.fields[0])) {
                    evalLeaf_mut = evalLeaf;
                    l_mut = l.fields[1];
                    continue Lattice_eval;
                }
                else {
                    return false;
                }
            case 5:
                if (Lattice_eval(evalLeaf, l.fields[0])) {
                    return true;
                }
                else {
                    evalLeaf_mut = evalLeaf;
                    l_mut = l.fields[1];
                    continue Lattice_eval;
                }
            default:
                return true;
        }
        break;
    }
}

export function Lattice_equivalent(a, b, evalLeaf) {
    return Lattice_eval(evalLeaf, a) === Lattice_eval(evalLeaf, b);
}

export function SemanticOptimizer_intersectBounds(b1, b2, isMax) {
    let matchResult, b, x, y;
    if (b1 != null) {
        if (b2 != null) {
            matchResult = 1;
            x = b1;
            y = b2;
        }
        else {
            matchResult = 0;
            b = b1;
        }
    }
    else {
        matchResult = 0;
        b = b2;
    }
    switch (matchResult) {
        case 0:
            return b;
        default: {
            const xv = (x.tag === 1) ? x.fields[0] : x.fields[0];
            const yv = (y.tag === 1) ? y.fields[0] : y.fields[0];
            if (isMax) {
                if (compare(xv, yv) < 0) {
                    return x;
                }
                else if (compare(yv, xv) < 0) {
                    return y;
                }
                else {
                    let matchResult_1;
                    if (x.tag === 1) {
                        matchResult_1 = 0;
                    }
                    else if (y.tag === 1) {
                        matchResult_1 = 0;
                    }
                    else {
                        matchResult_1 = 1;
                    }
                    switch (matchResult_1) {
                        case 0:
                            return new Bound$1(1, [xv]);
                        default:
                            return new Bound$1(0, [xv]);
                    }
                }
            }
            else if (compare(xv, yv) > 0) {
                return x;
            }
            else if (compare(yv, xv) > 0) {
                return y;
            }
            else {
                let matchResult_2;
                if (x.tag === 1) {
                    matchResult_2 = 0;
                }
                else if (y.tag === 1) {
                    matchResult_2 = 0;
                }
                else {
                    matchResult_2 = 1;
                }
                switch (matchResult_2) {
                    case 0:
                        return new Bound$1(1, [xv]);
                    default:
                        return new Bound$1(0, [xv]);
                }
            }
        }
    }
}

export function SemanticOptimizer_simplify(l_mut) {
    SemanticOptimizer_simplify:
    while (true) {
        const l = l_mut;
        let maxB, minB, minV, maxV;
        let matchResult, x_2, y_2, x_3, y_3, f1_1, f2_1, max1_1, max2_1, min1_1, min2_1, a, b, a_1, b_1, inner;
        switch (l.tag) {
            case 4: {
                if (l.fields[0].tag === 2) {
                    if (l.fields[1].tag === 2) {
                        if (equals_1(l.fields[0].fields[0], l.fields[1].fields[0])) {
                            matchResult = 0;
                            x_2 = l.fields[0].fields[0];
                            y_2 = l.fields[1].fields[0];
                        }
                        else if (l.fields[1].fields[0].tag === 12) {
                            if (l.fields[1].fields[0].fields[1].tag === 0) {
                                if (l.fields[0].fields[0].tag === 12) {
                                    if (l.fields[0].fields[0].fields[1].tag === 0) {
                                        if (l.fields[0].fields[0].fields[0] === l.fields[1].fields[0].fields[0]) {
                                            matchResult = 2;
                                            f1_1 = l.fields[0].fields[0].fields[0];
                                            f2_1 = l.fields[1].fields[0].fields[0];
                                            max1_1 = l.fields[0].fields[0].fields[1].fields[1];
                                            max2_1 = l.fields[1].fields[0].fields[1].fields[1];
                                            min1_1 = l.fields[0].fields[0].fields[1].fields[0];
                                            min2_1 = l.fields[1].fields[0].fields[1].fields[0];
                                        }
                                        else {
                                            matchResult = 3;
                                            a = l.fields[0];
                                            b = l.fields[1];
                                        }
                                    }
                                    else {
                                        matchResult = 3;
                                        a = l.fields[0];
                                        b = l.fields[1];
                                    }
                                }
                                else {
                                    matchResult = 3;
                                    a = l.fields[0];
                                    b = l.fields[1];
                                }
                            }
                            else {
                                matchResult = 3;
                                a = l.fields[0];
                                b = l.fields[1];
                            }
                        }
                        else {
                            matchResult = 3;
                            a = l.fields[0];
                            b = l.fields[1];
                        }
                    }
                    else {
                        matchResult = 3;
                        a = l.fields[0];
                        b = l.fields[1];
                    }
                }
                else {
                    matchResult = 3;
                    a = l.fields[0];
                    b = l.fields[1];
                }
                break;
            }
            case 5: {
                if (l.fields[0].tag === 2) {
                    if (l.fields[1].tag === 2) {
                        if (equals_1(l.fields[0].fields[0], l.fields[1].fields[0])) {
                            matchResult = 1;
                            x_3 = l.fields[0].fields[0];
                            y_3 = l.fields[1].fields[0];
                        }
                        else {
                            matchResult = 4;
                            a_1 = l.fields[0];
                            b_1 = l.fields[1];
                        }
                    }
                    else {
                        matchResult = 4;
                        a_1 = l.fields[0];
                        b_1 = l.fields[1];
                    }
                }
                else {
                    matchResult = 4;
                    a_1 = l.fields[0];
                    b_1 = l.fields[1];
                }
                break;
            }
            case 3: {
                matchResult = 5;
                inner = l.fields[0];
                break;
            }
            default:
                matchResult = 6;
        }
        switch (matchResult) {
            case 0:
                return new Lattice$1(2, [x_2]);
            case 1:
                return new Lattice$1(2, [x_3]);
            case 2: {
                const newMin = SemanticOptimizer_intersectBounds(min1_1, min2_1, false);
                const newMax = SemanticOptimizer_intersectBounds(max1_1, max2_1, true);
                if ((newMin != null) ? ((newMax != null) ? ((maxB = newMax, (minB = newMin, (minV = ((minB.tag === 1) ? minB.fields[0] : minB.fields[0]), (maxV = ((maxB.tag === 1) ? maxB.fields[0] : maxB.fields[0]), (compare(minV, maxV) > 0) ? false : (equals(minV, maxV) ? ((minB.tag === 0) && (maxB.tag === 0)) : true)))))) : true) : true) {
                    return new Lattice$1(2, [new Constraint(12, [f1_1, new Constraint(0, [newMin, newMax])])]);
                }
                else {
                    return new Lattice$1(1, []);
                }
            }
            case 3: {
                const sa = SemanticOptimizer_simplify(a);
                const sb = SemanticOptimizer_simplify(b);
                if (equals_1(sa, sb)) {
                    return sa;
                }
                else if (equals_1(sa, new Lattice$1(1, [])) ? true : equals_1(sb, new Lattice$1(1, []))) {
                    return new Lattice$1(1, []);
                }
                else if (equals_1(sa, new Lattice$1(0, []))) {
                    return sb;
                }
                else if (equals_1(sb, new Lattice$1(0, []))) {
                    return sa;
                }
                else {
                    let matchResult_1, f1_3, f2_3, max1_3, max2_3, min1_3, min2_3;
                    if (sa.tag === 2) {
                        if (sa.fields[0].tag === 12) {
                            if (sa.fields[0].fields[1].tag === 0) {
                                if (sb.tag === 2) {
                                    if (sb.fields[0].tag === 12) {
                                        if (sb.fields[0].fields[1].tag === 0) {
                                            if (sa.fields[0].fields[0] === sb.fields[0].fields[0]) {
                                                matchResult_1 = 0;
                                                f1_3 = sa.fields[0].fields[0];
                                                f2_3 = sb.fields[0].fields[0];
                                                max1_3 = sa.fields[0].fields[1].fields[1];
                                                max2_3 = sb.fields[0].fields[1].fields[1];
                                                min1_3 = sa.fields[0].fields[1].fields[0];
                                                min2_3 = sb.fields[0].fields[1].fields[0];
                                            }
                                            else {
                                                matchResult_1 = 1;
                                            }
                                        }
                                        else {
                                            matchResult_1 = 1;
                                        }
                                    }
                                    else {
                                        matchResult_1 = 1;
                                    }
                                }
                                else {
                                    matchResult_1 = 1;
                                }
                            }
                            else {
                                matchResult_1 = 1;
                            }
                        }
                        else {
                            matchResult_1 = 1;
                        }
                    }
                    else {
                        matchResult_1 = 1;
                    }
                    switch (matchResult_1) {
                        case 0: {
                            l_mut = (new Lattice$1(4, [sa, sb]));
                            continue SemanticOptimizer_simplify;
                        }
                        default:
                            return new Lattice$1(4, [sa, sb]);
                    }
                }
            }
            case 4: {
                const sa_1 = SemanticOptimizer_simplify(a_1);
                const sb_1 = SemanticOptimizer_simplify(b_1);
                if (equals_1(sa_1, sb_1)) {
                    return sa_1;
                }
                else if (equals_1(sa_1, new Lattice$1(0, [])) ? true : equals_1(sb_1, new Lattice$1(0, []))) {
                    return new Lattice$1(0, []);
                }
                else if (equals_1(sa_1, new Lattice$1(1, []))) {
                    return sb_1;
                }
                else if (equals_1(sb_1, new Lattice$1(1, []))) {
                    return sa_1;
                }
                else {
                    return new Lattice$1(5, [sa_1, sb_1]);
                }
            }
            case 5: {
                const sInner = SemanticOptimizer_simplify(inner);
                switch (sInner.tag) {
                    case 0:
                        return new Lattice$1(1, []);
                    case 1:
                        return new Lattice$1(0, []);
                    default:
                        return new Lattice$1(3, [sInner]);
                }
            }
            default:
                return l;
        }
        break;
    }
}

/**
 * Phantom-typed query shell.
 */
export class Query$2 extends Record {
    constructor(Predicate) {
        super();
        this.Predicate = Predicate;
    }
}

export function Query$2_$reflection(gen0, gen1) {
    return record_type("Canon.Core.Query`2", [gen0, gen1], Query$2, () => [["Predicate", Lattice$1_$reflection(gen1)]]);
}

