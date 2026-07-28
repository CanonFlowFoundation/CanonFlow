
import { Union, Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { lambda_type, unit_type, union_type, int32_type, bool_type, record_type, list_type, option_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Lattice$1_$reflection, Constraint_$reflection, FieldKind_$reflection } from "../Canon.Core/Lattice.js";

/**
 * Represents semantic metadata attached to a field (from Helios concepts)
 */
export class SemanticMetadata extends Record {
    constructor(Label, Description, Synonyms, Metrics, FieldKind) {
        super();
        this.Label = Label;
        this.Description = Description;
        this.Synonyms = Synonyms;
        this.Metrics = Metrics;
        this.FieldKind = FieldKind;
    }
}

export function SemanticMetadata_$reflection() {
    return record_type("Canon.Introspect.SemanticMetadata", [], SemanticMetadata, () => [["Label", option_type(string_type)], ["Description", option_type(string_type)], ["Synonyms", list_type(string_type)], ["Metrics", list_type(string_type)], ["FieldKind", option_type(FieldKind_$reflection())]]);
}

export class ForeignKeyDef extends Record {
    constructor(ColumnName, RefTable, RefColumn) {
        super();
        this.ColumnName = ColumnName;
        this.RefTable = RefTable;
        this.RefColumn = RefColumn;
    }
}

export function ForeignKeyDef_$reflection() {
    return record_type("Canon.Introspect.ForeignKeyDef", [], ForeignKeyDef, () => [["ColumnName", string_type], ["RefTable", string_type], ["RefColumn", string_type]]);
}

export class IndexDef extends Record {
    constructor(Name, Columns, IsUnique) {
        super();
        this.Name = Name;
        this.Columns = Columns;
        this.IsUnique = IsUnique;
    }
}

export function IndexDef_$reflection() {
    return record_type("Canon.Introspect.IndexDef", [], IndexDef, () => [["Name", string_type], ["Columns", list_type(string_type)], ["IsUnique", bool_type]]);
}

/**
 * Represents a column in the database schema.
 */
export class ColumnDef extends Record {
    constructor(Name, DataType, IsNullable, IsPrimaryKey, DefaultValue, IsGenerated, Description, MaxLength, CheckConstraints, ParsedConstraints, Semantics) {
        super();
        this.Name = Name;
        this.DataType = DataType;
        this.IsNullable = IsNullable;
        this.IsPrimaryKey = IsPrimaryKey;
        this.DefaultValue = DefaultValue;
        this.IsGenerated = IsGenerated;
        this.Description = Description;
        this.MaxLength = MaxLength;
        this.CheckConstraints = CheckConstraints;
        this.ParsedConstraints = ParsedConstraints;
        this.Semantics = Semantics;
    }
}

export function ColumnDef_$reflection() {
    return record_type("Canon.Introspect.ColumnDef", [], ColumnDef, () => [["Name", string_type], ["DataType", string_type], ["IsNullable", bool_type], ["IsPrimaryKey", bool_type], ["DefaultValue", option_type(string_type)], ["IsGenerated", bool_type], ["Description", option_type(string_type)], ["MaxLength", option_type(int32_type)], ["CheckConstraints", list_type(string_type)], ["ParsedConstraints", list_type(Lattice$1_$reflection(Constraint_$reflection()))], ["Semantics", option_type(SemanticMetadata_$reflection())]]);
}

export class TableType extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Table", "View", "MaterializedView"];
    }
}

export function TableType_$reflection() {
    return union_type("Canon.Introspect.TableType", [], TableType, () => [[], [], []]);
}

/**
 * Represents a table or view in the database schema.
 */
export class TableDef extends Record {
    constructor(Schema, Name, Type, Description, Columns, PrimaryKeys, ForeignKeys, Indexes, TableConstraints) {
        super();
        this.Schema = Schema;
        this.Name = Name;
        this.Type = Type;
        this.Description = Description;
        this.Columns = Columns;
        this.PrimaryKeys = PrimaryKeys;
        this.ForeignKeys = ForeignKeys;
        this.Indexes = Indexes;
        this.TableConstraints = TableConstraints;
    }
}

export function TableDef_$reflection() {
    return record_type("Canon.Introspect.TableDef", [], TableDef, () => [["Schema", string_type], ["Name", string_type], ["Type", TableType_$reflection()], ["Description", option_type(string_type)], ["Columns", list_type(ColumnDef_$reflection())], ["PrimaryKeys", list_type(string_type)], ["ForeignKeys", list_type(ForeignKeyDef_$reflection())], ["Indexes", list_type(IndexDef_$reflection())], ["TableConstraints", list_type(string_type)]]);
}

/**
 * Abstraction for database drivers to provide their schema and constraints.
 * SqlHydra will implement this for Postgres/DuckDB/SQLite.
 */
export class SchemaProvider extends Record {
    constructor(Harvest) {
        super();
        this.Harvest = Harvest;
    }
}

export function SchemaProvider_$reflection() {
    return record_type("Canon.Introspect.SchemaProvider", [], SchemaProvider, () => [["Harvest", lambda_type(unit_type, list_type(TableDef_$reflection()))]]);
}

