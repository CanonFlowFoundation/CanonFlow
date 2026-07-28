
import { Divergence, FidelityModule_combine, Fidelity } from "../Canon.Core/Lineage.js";
import { printf, toText, join, concat } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { map } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { Lattice$1 } from "../Canon.Core/Lattice.js";

export function toOpenApiSchema(predicate) {
    let clo, clo_1;
    switch (predicate.tag) {
        case 1:
            return ["{\"not\": {}}", new Fidelity(0, [])];
        case 3: {
            const patternInput = toOpenApiSchema(predicate.fields[0]);
            return [concat("{\"not\": ", patternInput[0], "}"), patternInput[1]];
        }
        case 4: {
            const patternInput_1 = toOpenApiSchema(predicate.fields[0]);
            const patternInput_2 = toOpenApiSchema(predicate.fields[1]);
            return [`{"allOf": [${patternInput_1[0]}, ${patternInput_2[0]}]}`, FidelityModule_combine(patternInput_1[1], patternInput_2[1])];
        }
        case 5: {
            const patternInput_3 = toOpenApiSchema(predicate.fields[0]);
            const patternInput_4 = toOpenApiSchema(predicate.fields[1]);
            return [`{"anyOf": [${patternInput_3[0]}, ${patternInput_4[0]}]}`, FidelityModule_combine(patternInput_3[1], patternInput_4[1])];
        }
        case 2: {
            const c = predicate.fields[0];
            let matchResult, v, v_1, v_2, v_3, len, items, items_1, colA, colB, op, raw, field, inner_1;
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
                    raw = c.fields[0];
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
                    return ["{\"type\": \"null\"}", new Fidelity(0, [])];
                case 1:
                    return ["{}", new Fidelity(0, [])];
                case 2:
                    return [`{"exclusiveMinimum": ${v}}`, new Fidelity(0, [])];
                case 3:
                    return [`{"exclusiveMaximum": ${v_1}}`, new Fidelity(0, [])];
                case 4:
                    return [`{"minimum": ${v_2}}`, new Fidelity(0, [])];
                case 5:
                    return [`{"maximum": ${v_3}}`, new Fidelity(0, [])];
                case 6:
                    return ["{}", new Fidelity(2, [new Divergence(1, ["Complex range bounds not fully implemented in OpenAPI schema"])])];
                case 7:
                    return ["{\"type\": \"integer\"}", new Fidelity(2, [new Divergence(1, ["Int range requires precision bounds"])])];
                case 8:
                    return ["{}", new Fidelity(2, [new Divergence(1, ["String range collation may differ and cannot be represented in OpenAPI"])])];
                case 9:
                    return [`{"maxLength": ${len}}`, new Fidelity(0, [])];
                case 10:
                    return [concat("{\"enum\": [", join(", ", map((clo = toText(printf("\"%s\"")), clo), items)), "]}"), new Fidelity(0, [])];
                case 11:
                    return [concat("{\"enum\": [", join(", ", map((clo_1 = toText(printf("\"%s\"")), clo_1), items_1)), "]}"), new Fidelity(0, [])];
                case 12:
                    return [`{"description": "Requires ${colA} ${op} ${colB}"}`, new Fidelity(2, [new Divergence(1, ["OpenAPI maps cross-field validation to description strings"])])];
                case 13:
                    return ["{}", new Fidelity(5, ["PrimaryKey concept does not exist in OpenAPI validators"])];
                case 14:
                    return ["{\"minLength\": 1}", new Fidelity(0, [])];
                case 15:
                    return ["{}", new Fidelity(5, [concat("Cannot transpile raw SQL to OpenAPI: ", raw)])];
                default: {
                    const patternInput_5 = toOpenApiSchema(new Lattice$1(2, [inner_1]));
                    return [`{"properties": {"${field}": ${patternInput_5[0]}}`, patternInput_5[1]];
                }
            }
        }
        default:
            return ["{}", new Fidelity(0, [])];
    }
}

export function emitSchema(name, predicate) {
    const patternInput = toOpenApiSchema(predicate);
    return [(`
"${name}": ${patternInput[0]}
`).trim(), patternInput[1]];
}

