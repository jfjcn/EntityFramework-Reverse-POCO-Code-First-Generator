﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Efrpg.Filtering;
using Efrpg.LanguageMapping;

namespace Efrpg.Readers
{
    public abstract class DatabaseReader
    {
        private readonly DbProviderFactory _factory;
        protected IDatabaseReaderPlugin DatabaseReaderPlugin;
        protected readonly StringBuilder DatabaseDetails;
        protected Dictionary<string, string> StoredProcedureParameterDbType; // [SQL Data Type] = SqlDbType. (For consistent naming)
        protected Dictionary<string, string> DbTypeToPropertyType; // [SQL Data Type] = Language type.

        protected string DatabaseEdition, DatabaseEngineEdition, DatabaseProductVersion;
        protected int DatabaseProductMajorVersion;

        public bool IncludeSchema { get; protected set; }
        public bool DoNotSpecifySizeForMaxLength { get; protected set; }

        protected abstract string TableSQL();
        protected abstract string ForeignKeySQL();
        protected abstract string ExtendedPropertySQL();
        protected abstract string DoesExtendedPropertyTableExistSQL();
        protected abstract string IndexSQL();
        public abstract bool CanReadStoredProcedures();
        protected abstract string StoredProcedureSQL();
        protected abstract string ReadDatabaseEditionSQL();
        protected abstract string MultiContextSQL();
        protected abstract string EnumSQL(string table, string nameField, string valueField);

        // Synonym
        protected abstract string SynonymTableSQLSetup();
        protected abstract string SynonymTableSQL();
        protected abstract string SynonymForeignKeySQLSetup();
        protected abstract string SynonymForeignKeySQL();
        protected abstract string SynonymStoredProcedureSQLSetup();
        protected abstract string SynonymStoredProcedureSQL();

        // Database specific flags
        protected abstract string SpecialQueryFlags();

        // Stored proc return objects
        public abstract void ReadStoredProcReturnObjects(List<StoredProcedure> procs);

        protected DatabaseReader(DbProviderFactory factory, IDatabaseToPropertyType databaseToPropertyType)
        {
            if (databaseToPropertyType == null)
                databaseToPropertyType = new SqlServerToCSharp(); // Default. Can be overridden in PluginDatabaseReader

            DbTypeToPropertyType         = databaseToPropertyType.GetMapping();
            DatabaseEdition              = null;
            DatabaseEngineEdition        = null;
            DatabaseProductVersion       = null;
            _factory                     = factory;
            DatabaseReaderPlugin         = null;
            IncludeSchema                = true;
            DoNotSpecifySizeForMaxLength = false;
            DatabaseDetails              = new StringBuilder(255);
        }

        // Any special setup required
        public virtual void Init()
        {
            if (!string.IsNullOrEmpty(DatabaseEdition))
                return;

            var sql = ReadDatabaseEditionSQL();
            if (string.IsNullOrEmpty(sql))
                return;

            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();
                var cmd = GetCmd(conn);
                if (cmd == null)
                    return;

                cmd.CommandText = sql;

                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        DatabaseEdition             = rdr["Edition"].ToString();
                        DatabaseEngineEdition       = rdr["EngineEdition"].ToString();
                        DatabaseProductVersion      = rdr["ProductVersion"].ToString();
                        DatabaseProductMajorVersion = int.Parse(DatabaseProductVersion.Substring(0, 2).Replace(".", string.Empty));

                        DatabaseDetails.AppendLine("// Database Edition       : " + DatabaseEdition);
                        DatabaseDetails.AppendLine("// Database Engine Edition: " + DatabaseEngineEdition);
                        DatabaseDetails.AppendLine("// Database Version       : " + DatabaseProductVersion);
                    }
                }
            }
        }

        private static readonly Regex ReservedColumnNames = new Regex("^(event|Equals|GetHashCode|GetType|ToString)$", RegexOptions.Compiled);

        public static readonly List<string> ReservedKeywords = new List<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "volatile",
            "void", "while"
        };

        public string GetDatabaseDetails()
        {
            return DatabaseDetails.ToString();
        }

        // Maps database type to language type. i.e. for C#, would map 'varchar' to 'string'
        public string GetPropertyType(string dbType)
        {
            string propertyType;
            if (DbTypeToPropertyType.TryGetValue(dbType, out propertyType))
                return propertyType;

            return DbTypeToPropertyType[string.Empty]; // return default, which is usually string
        }

        // Type converter
        public string GetStoredProcedureParameterDbType(string sqlType)
        {
            if (StoredProcedureParameterDbType == null)
                return string.Empty;

            string parameterDbType;
            if (StoredProcedureParameterDbType.TryGetValue(sqlType, out parameterDbType))
                return parameterDbType;

            return StoredProcedureParameterDbType[string.Empty]; // return default, which is usually VarChar
        }

        protected DbCommand GetCmd(DbConnection connection)
        {
            if (connection == null)
                return null;

            var cmd = _factory.CreateCommand();
            if (cmd == null)
                return null;

            cmd.Connection = connection;
            if(Settings.DatabaseType != DatabaseType.SqlCe)
                cmd.CommandTimeout = Settings.CommandTimeout;

            return cmd;
        }

        public List<RawTable> ReadTables(bool includeSynonyms)
        {
            if (DatabaseReaderPlugin != null)
                return DatabaseReaderPlugin.ReadTables();

            var result = new List<RawTable>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                string sql;
                if (includeSynonyms && Settings.DatabaseType != DatabaseType.SqlCe)
                    sql = SynonymTableSQLSetup() + TableSQL() + SynonymTableSQL() + SpecialQueryFlags();
                else
                    sql = TableSQL() + SpecialQueryFlags();

                var temporalTableSupport = DatabaseProductMajorVersion >= 13;
                if (!temporalTableSupport)
                {
                    // Replace the column names (only present in SQL Server 2016 or later) with literal constants so the query works with older versions of SQL Server.
                    sql = sql
                        .Replace("[sc].[generated_always_type]", "0")
                        .Replace("[c].[generated_always_type]", "0")
                        .Replace("[st].[temporal_type]", "0");
                }

                cmd.CommandText = sql;

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var table = new RawTable(
                            rdr["SchemaName"].ToString().Trim(),
                            rdr["TableName"].ToString().Trim(),
                            string.Compare(rdr["TableType"].ToString().Trim(), "View", StringComparison.OrdinalIgnoreCase) == 0,
                            (int) rdr["Scale"],
                            rdr["TypeName"].ToString().Trim().ToLower(),
                            (bool) rdr["IsNullable"],
                            (int) rdr["MaxLength"],
                            (int) rdr["DateTimePrecision"],
                            (int) rdr["Precision"],
                            (bool) rdr["IsIdentity"],
                            (bool) rdr["IsComputed"],
                            (bool) rdr["IsRowGuid"],
                            (byte) rdr["GeneratedAlwaysType"],
                            (bool) rdr["IsStoreGenerated"],
                            (int) rdr["PrimaryKeyOrdinal"],
                            (bool) rdr["PrimaryKey"],
                            (bool) rdr["IsForeignKey"],
                            (int) rdr["Ordinal"],
                            rdr["ColumnName"].ToString().Trim(),
                            rdr["Default"].ToString().Trim()
                        );

                        result.Add(table);
                    }
                }
            }

            return result;
        }
        
        public List<RawForeignKey> ReadForeignKeys(bool includeSynonyms)
        {
            if (DatabaseReaderPlugin != null)
                return DatabaseReaderPlugin.ReadForeignKeys();

            var result = new List<RawForeignKey>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                if (includeSynonyms)
                    cmd.CommandText = SynonymForeignKeySQLSetup() + ForeignKeySQL() + SynonymForeignKeySQL() + SpecialQueryFlags();
                else
                    cmd.CommandText = ForeignKeySQL() + SpecialQueryFlags();

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var fk = new RawForeignKey(
                            rdr["Constraint_Name"].ToString(),
                            null, // ParentName is null, therefore it will be generated
                            null, // ChildName  is null, therefore it will be generated
                            rdr["PK_Column"].ToString(),
                            rdr["FK_Column"].ToString(),
                            rdr["pkSchema"].ToString(),
                            rdr["PK_Table"].ToString(),
                            rdr["fkSchema"].ToString(),
                            rdr["FK_Table"].ToString(),
                            (int) rdr["ORDINAL_POSITION"],
                            ((int) rdr["CascadeOnDelete"]) == 1,
                            (bool) rdr["IsNotEnforced"],
                            false
                        );

                        result.Add(fk);
                    }
                }
            }

            return result;
        }

        public List<RawIndex> ReadIndexes()
        {
            if (DatabaseReaderPlugin != null)
                return DatabaseReaderPlugin.ReadIndexes();

            var result = new List<RawIndex>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                var sql = IndexSQL();
                if (string.IsNullOrWhiteSpace(sql))
                    return result;

                cmd.CommandText = sql + SpecialQueryFlags();

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var index = new RawIndex
                        (
                            rdr["TableSchema"].ToString().Trim(),
                            rdr["TableName"].ToString().Trim(),
                            rdr["IndexName"].ToString().Trim(),
                            (byte) rdr["KeyOrdinal"],
                            rdr["ColumnName"].ToString().Trim(),
                            (int) rdr["ColumnCount"],
                            (bool) rdr["IsUnique"],
                            (bool) rdr["IsPrimaryKey"],
                            (bool) rdr["IsUniqueConstraint"],
                            ((int) rdr["IsClustered"]) == 1
                        );

                        result.Add(index);
                    }
                }
            }

            return result;
        }

        public List<RawExtendedProperty> ReadExtendedProperties()
        {
            if (DatabaseReaderPlugin != null)
                return DatabaseReaderPlugin.ReadExtendedProperties();

            var result = new List<RawExtendedProperty>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                var extendedPropertySQL = ExtendedPropertySQL();
                if (string.IsNullOrEmpty(extendedPropertySQL))
                    return result;

                // Check if any SQL is returned. If so, run it. (Specific to SqlCE)
                var doesExtendedPropertyTableExistSQL = DoesExtendedPropertyTableExistSQL();
                if (!string.IsNullOrEmpty(doesExtendedPropertyTableExistSQL))
                {
                    cmd.CommandText = doesExtendedPropertyTableExistSQL;
                    var obj = cmd.ExecuteScalar();
                    if (obj == null)
                        return result; // No extended properties table
                }

                cmd.CommandText = extendedPropertySQL + SpecialQueryFlags();

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var extendedProperty = rdr["property"].ToString().Trim();
                        if (string.IsNullOrEmpty(extendedProperty))
                            continue;

                        var rep = new RawExtendedProperty
                        (
                            rdr["schema"].ToString().Trim(),
                            rdr["table"] .ToString().Trim(),
                            rdr["column"].ToString().Trim(),
                            extendedProperty
                        );

                        result.Add(rep);
                    }
                }
            }

            return result;
        }

        public List<RawStoredProcedure> ReadStoredProcs(bool includeSynonyms)
        {
            if (DatabaseReaderPlugin != null)
                return DatabaseReaderPlugin.ReadStoredProcs();

            var result = new List<RawStoredProcedure>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var storedProcedureSQL = StoredProcedureSQL();
                if (string.IsNullOrEmpty(storedProcedureSQL))
                    return result;

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                if (includeSynonyms)
                    cmd.CommandText = SynonymStoredProcedureSQLSetup() + storedProcedureSQL + SynonymStoredProcedureSQL() + SpecialQueryFlags();
                else
                    cmd.CommandText = storedProcedureSQL + SpecialQueryFlags();

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var schema         = rdr["SPECIFIC_SCHEMA"] .ToString().Trim();
                        var name           = rdr["SPECIFIC_NAME"]   .ToString().Trim();
                        var routineType    = rdr["ROUTINE_TYPE"]    .ToString().Trim().ToLower();
                        var returnDataType = rdr["RETURN_DATA_TYPE"].ToString().Trim().ToLower();
                        var dataType       = rdr["DATA_TYPE"]       .ToString().Trim().ToLower();
                        var parameterMode  = rdr["PARAMETER_MODE"]  .ToString().Trim().ToLower();

                        var isTableValuedFunction  = (routineType == "function" && returnDataType == "table");
                        var isScalarValuedFunction = (routineType == "function" && returnDataType != "table");
                        var isStoredProcedure      = (routineType == "procedure");

                        StoredProcedureParameter parameter = null;
                        if (rdr["DATA_TYPE"] != null && rdr["DATA_TYPE"] != DBNull.Value)
                        {
                            parameter = new StoredProcedureParameter
                            {
                                Ordinal             = (int) rdr["ORDINAL_POSITION"],
                                Name                = rdr["PARAMETER_NAME"].ToString().Trim(),
                                SqlDbType           = GetStoredProcedureParameterDbType(dataType),
                                ReturnSqlDbType     = GetStoredProcedureParameterDbType(returnDataType),
                                PropertyType        = GetPropertyType(dataType),
                                ReturnPropertyType  = GetPropertyType(returnDataType),
                                DateTimePrecision   = (short) rdr["DATETIME_PRECISION"],
                                MaxLength           = (int) rdr["CHARACTER_MAXIMUM_LENGTH"],
                                Precision           = (byte) rdr["NUMERIC_PRECISION"],
                                Scale               = (int) rdr["NUMERIC_SCALE"],
                                UserDefinedTypeName = rdr["USER_DEFINED_TYPE"].ToString().Trim()
                            };

                            switch (parameterMode)
                            {
                                case "in":
                                    parameter.Mode = StoredProcedureParameterMode.In;
                                    break;

                                case "out":
                                    parameter.Mode = StoredProcedureParameterMode.Out;
                                    break;

                                default:
                                    parameter.Mode = StoredProcedureParameterMode.InOut;
                                    break;
                            }

                            var clean = CleanUp(parameter.Name.Replace("@", string.Empty));
                            if (!string.IsNullOrEmpty(clean))
                            {
                                parameter.NameHumanCase = Inflector.MakeInitialLower((Settings.UsePascalCase ? Inflector.ToTitleCase(clean) : clean).Replace(" ", ""));

                                if (ReservedKeywords.Contains(parameter.NameHumanCase))
                                    parameter.NameHumanCase = "@" + parameter.NameHumanCase;
                            }
                        }

                        var rsp = new RawStoredProcedure(schema, name, isTableValuedFunction, isScalarValuedFunction, isStoredProcedure, parameter);
                        result.Add(rsp);
                    }
                }
            }
            return result;
        }

        public List<MultiContextSettings> ReadMultiContextSettings()
        {
            var result = new List<MultiContextSettings>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = string.IsNullOrWhiteSpace(Settings.MultiContextSettingsConnectionString) ? Settings.ConnectionString : Settings.MultiContextSettingsConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                var sql = MultiContextSQL();
                if (string.IsNullOrWhiteSpace(sql))
                    return result;

                cmd.CommandText = MultiContextSQL();

                var contextMap = new Dictionary<int, MultiContextSettings>();
                var tableMap   = new Dictionary<int, MultiContextTableSettings>();

                using (var rdr = cmd.ExecuteReader())
                {
                    // Contexts
                    while (rdr.Read())
                    {
                        var contextId = GetReaderInt(rdr, "Id");
                        if (!contextId.HasValue)
                            continue; // Cannot use context

                        var c = new MultiContextSettings
                        {
                            // Store standard fields
                            Name         = GetReaderString(rdr, "Name"),
                            Namespace    = GetReaderString(rdr, "Namespace"),
                            Description  = GetReaderString(rdr, "Description"),
                            BaseSchema   = GetReaderString(rdr, "BaseSchema"),
                            TemplatePath = GetReaderString(rdr, "TemplatePath"),
                            Filename     = GetReaderString(rdr, "Filename"),
                            AllFields    = ReadAllFields(rdr),

                            Tables           = new List<MultiContextTableSettings>(),
                            StoredProcedures = new List<MultiContextStoredProcedureSettings>(),
                            Enumerations     = new List<EnumerationSettings>(),
                            Functions        = new List<MultiContextFunctionSettings>(),
                            ForeignKeys      = new List<MultiContextForeignKeySettings>()
                        };

                        contextMap.Add(contextId.Value, c);
                        result.Add(c);
                    }

                    if (!result.Any())
                        return result;

                    // Tables
                    rdr.NextResult();
                    MultiContextSettings context;
                    while (rdr.Read())
                    {
                        var tableId = GetReaderInt(rdr, "Id");
                        if (!tableId.HasValue)
                            continue; // Cannot use table

                        var contextId = GetReaderInt(rdr, "ContextId");
                        if (!contextId.HasValue)
                            continue; // No context

                        if(!contextMap.ContainsKey(contextId.Value))
                            continue; // Context not found

                        context = contextMap[contextId.Value];

                        var t = new MultiContextTableSettings
                        {
                            Name          = GetReaderString(rdr, "Name"),
                            Description   = GetReaderString(rdr, "Description"),
                            PluralName    = GetReaderString(rdr, "PluralName"),
                            DbName        = GetReaderString(rdr, "DbName"),
                            Attributes    = GetReaderString(rdr, "Attributes"),
                            DbSetModifier = GetReaderString(rdr, "DbSetModifier"),
                            AllFields     = ReadAllFields(rdr),

                            Columns = new List<MultiContextColumnSettings>()
                        };

                        tableMap.Add(tableId.Value, t);
                        context.Tables.Add(t);
                    }
                    
                    // Columns
                    rdr.NextResult();
                    while (rdr.Read())
                    {
                        var tableId = GetReaderInt(rdr, "TableId");
                        if (tableId == null)
                            continue; // Cannot use column as not associated to a table

                        if(!tableMap.ContainsKey(tableId.Value))
                            continue; // Table not found

                        var table = tableMap[tableId.Value];

                        var col = new MultiContextColumnSettings
                        {
                            Name             = GetReaderString(rdr, "Name"),
                            DbName           = GetReaderString(rdr, "DbName"),
                            IsPrimaryKey     = GetReaderBool(rdr,   "IsPrimaryKey"),
                            OverrideModifier = GetReaderBool(rdr,   "OverrideModifier"),
                            EnumType         = GetReaderString(rdr, "EnumType"),
                            Attributes       = GetReaderString(rdr, "Attributes"),
                            PropertyType     = GetReaderString(rdr, "PropertyType"),
                            IsNullable       = GetReaderBool(rdr, "IsNullable"),
                            AllFields        = ReadAllFields(rdr)
                        };

                        table.Columns.Add(col);
                    }

                    // Stored Procedures
                    rdr.NextResult();
                    while (rdr.Read())
                    {
                        var contextId = GetReaderInt(rdr, "ContextId");
                        if (!contextId.HasValue)
                            continue; // No context

                        if (!contextMap.ContainsKey(contextId.Value))
                            continue; // Context not found

                        context = contextMap[contextId.Value];

                        var sp = new MultiContextStoredProcedureSettings
                        {
                            Name        = GetReaderString(rdr, "Name"),
                            DbName      = GetReaderString(rdr, "DbName"),
                            ReturnModel = GetReaderString(rdr, "ReturnModel"),
                            AllFields   = ReadAllFields(rdr)
                        };

                        context.StoredProcedures.Add(sp);
                    }

                    // Functions
                    rdr.NextResult();
                    while (rdr.Read())
                    {
                        var contextId = GetReaderInt(rdr, "ContextId");
                        if (!contextId.HasValue)
                            continue; // No context

                        if (!contextMap.ContainsKey(contextId.Value))
                            continue; // Context not found

                        context = contextMap[contextId.Value];

                        var f = new MultiContextFunctionSettings
                        {
                            Name      = GetReaderString(rdr, "Name"),
                            DbName    = GetReaderString(rdr, "DbName"),
                            AllFields = ReadAllFields(rdr)
                        };

                        context.Functions.Add(f);
                    }

                    // Enumerations
                    rdr.NextResult();
                    while (rdr.Read())
                    {
                        var contextId = GetReaderInt(rdr, "ContextId");
                        if (!contextId.HasValue)
                            continue; // No context

                        if (!contextMap.ContainsKey(contextId.Value))
                            continue; // Context not found

                        context = contextMap[contextId.Value];

                        var e = new EnumerationSettings
                        {
                            Name       = GetReaderString(rdr, "Name"),
                            Table      = GetReaderString(rdr, "Table"),
                            NameField  = GetReaderString(rdr, "NameField"),
                            ValueField = GetReaderString(rdr, "ValueField"),
                            AllFields  = ReadAllFields(rdr)
                        };

                        context.Enumerations.Add(e);
                    }

                    // Foreign keys
                    rdr.NextResult();
                    while (rdr.Read())
                    {
                        var contextId = GetReaderInt(rdr, "ContextId");
                        if (!contextId.HasValue)
                            continue; // No context

                        if (!contextMap.ContainsKey(contextId.Value))
                            continue; // Context not found

                        context = contextMap[contextId.Value];

                        var fk = new MultiContextForeignKeySettings
                        {
                            ConstraintName      = GetReaderString(rdr, "ConstraintName"),
                            ParentName          = GetReaderString(rdr, "ParentName"),
                            ChildName           = GetReaderString(rdr, "ChildName"),
                            PkSchema            = GetReaderString(rdr, "PkSchema") ?? context.BaseSchema,
                            PkTableName         = GetReaderString(rdr, "PkTableName"),
                            PkColumn            = GetReaderString(rdr, "PkColumn"),
                            FkSchema            = GetReaderString(rdr, "FkSchema") ?? context.BaseSchema,
                            FkTableName         = GetReaderString(rdr, "FkTableName"),
                            FkColumn            = GetReaderString(rdr, "FkColumn"),
                            Ordinal             = GetReaderInt(rdr,    "Ordinal") ?? 0,
                            CascadeOnDelete     = GetReaderBool(rdr,   "CascadeOnDelete") ?? false,
                            IsNotEnforced       = GetReaderBool(rdr,   "IsNotEnforced") ?? false,
                            HasUniqueConstraint = GetReaderBool(rdr,   "HasUniqueConstraint") ?? false
                        };

                        context.ForeignKeys.Add(fk);
                    }
                }
            }

            return result;
        }

        private Dictionary<string, object> ReadAllFields(DbDataReader rdr)
        {
            var result = new Dictionary<string, object>();

            for (var n = 0; n < rdr.FieldCount; ++n)
            {
                var o = rdr.GetValue(n);
                if(o != DBNull.Value)
                    result.Add(rdr.GetName(n), rdr.GetValue(n));
            }

            return result;
        }

        public List<Enumeration> ReadEnums(List<EnumerationSettings> enums)
        {
            var result = new List<Enumeration>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                foreach (var e in enums)
                {
                    var sql = EnumSQL(e.Table, e.NameField, e.ValueField);
                    if (string.IsNullOrEmpty(sql))
                        continue;

                    cmd.CommandText = sql;

                    try
                    {
                        using (var rdr = cmd.ExecuteReader())
                        {
                            var items = new List<KeyValuePair<string, string>>();
                            while (rdr.Read())
                            {
                                var name = rdr["NameField"].ToString().Trim();
                                if (string.IsNullOrEmpty(name))
                                    continue;

                                name = RemoveNonAlphanumerics.Replace(name, string.Empty);
                                name = Inflector.ToTitleCase(name).Replace(" ", "").Trim();
                                if (string.IsNullOrEmpty(name))
                                    continue;

                                var value = rdr["ValueField"].ToString().Trim();
                                if (string.IsNullOrEmpty(value))
                                    continue;

                                items.Add(new KeyValuePair<string, string>(name, value));
                            }

                            if(items.Any())
                                result.Add(new Enumeration(e.Name, items));
                        }
                    }
                    catch (Exception)
                    {
                        // Enum table does not exist in database, skip
                    }
                }
            }
            return result;
        }

        private static string GetReaderString(DbDataReader rdr, string name)
        {
            try
            {
                return rdr[name].ToString().Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int? GetReaderInt(DbDataReader rdr, string name)
        {
            try
            {
                return (int) rdr[name];
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool? GetReaderBool(DbDataReader rdr, string name)
        {
            try
            {
                return (bool) rdr[name];
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Column CreateColumn(RawTable rt, Table table, IDbContextFilter filter)
        {
            var col = new Column
            {
                Scale               = rt.Scale,
                PropertyType        = GetPropertyType(rt.TypeName),
                SqlPropertyType     = rt.TypeName,
                IsNullable          = rt.IsNullable,
                MaxLength           = rt.MaxLength,
                DateTimePrecision   = rt.DateTimePrecision,
                Precision           = rt.Precision,
                IsIdentity          = rt.IsIdentity,
                IsComputed          = rt.IsComputed,
                IsRowGuid           = rt.IsRowGuid,
                GeneratedAlwaysType = (ColumnGeneratedAlwaysType)rt.GeneratedAlwaysType,
                IsStoreGenerated    = rt.IsStoreGenerated,
                PrimaryKeyOrdinal   = rt.PrimaryKeyOrdinal,
                IsPrimaryKey        = rt.PrimaryKey,
                IsForeignKey        = rt.IsForeignKey,
                Ordinal             = rt.Ordinal,
                DbName                = rt.ColumnName,
                Default             = rt.Default,
                ParentTable         = table
            };

            if (col.MaxLength == -1 && (col.SqlPropertyType.EndsWith("varchar", StringComparison.InvariantCultureIgnoreCase) ||
                                        col.SqlPropertyType.EndsWith("varbinary", StringComparison.InvariantCultureIgnoreCase)))
            {
                col.SqlPropertyType += "(max)";
            }

            if (col.IsPrimaryKey && !col.IsIdentity && col.IsStoreGenerated && rt.TypeName == "uniqueidentifier")
            {
                col.IsStoreGenerated = false;
                col.IsIdentity = true;
            }

             if (!col.IsPrimaryKey && filter.IsExcluded(col))
                col.Hidden = true;

            col.IsFixedLength = (rt.TypeName == "char" || rt.TypeName == "nchar");
            col.IsUnicode     = !(rt.TypeName == "char" || rt.TypeName == "varchar" || rt.TypeName == "text");
            col.IsMaxLength   = (rt.TypeName == "ntext");

            col.IsRowVersion = col.IsStoreGenerated && !col.IsNullable && rt.TypeName == "timestamp";
            if (col.IsRowVersion)
                col.MaxLength = 8;

            if (rt.TypeName == "hierarchyid")
                col.MaxLength = 0;

            col.CleanUpDefault();
            col.NameHumanCase = CleanUp(col.DbName);
            col.NameHumanCase = ReservedColumnNames.Replace(col.NameHumanCase, "_$1");

            if (ReservedKeywords.Contains(col.NameHumanCase))
                col.NameHumanCase = "@" + col.NameHumanCase;

            col.DisplayName = Column.ToDisplayName(col.DbName);

            var titleCase = (Settings.UsePascalCase ? Inflector.ToTitleCase(col.NameHumanCase) : col.NameHumanCase).Replace(" ", string.Empty);
            if (titleCase != string.Empty)
                col.NameHumanCase = titleCase;

            // Make sure property name doesn't clash with class name
            if (col.NameHumanCase == table.NameHumanCase)
                col.NameHumanCase += "_";

            if (char.IsDigit(col.NameHumanCase[0]))
                col.NameHumanCase = "_" + col.NameHumanCase;

            table.HasNullableColumns = col.IsColumnNullable();

            // If PropertyType is empty, return null. Most likely ignoring a column due to legacy (such as OData not supporting spatial types)
            if (string.IsNullOrEmpty(col.PropertyType))
                return null;

            return col;
        }

        private static readonly Regex RemoveNonAlphanumerics = new Regex(@"[^\w\d\s_-]", RegexOptions.Compiled);

        public static readonly Func<string, string> CleanUp = (str) =>
        {
            // Replace punctuation and symbols in variable names as these are not allowed.
            var len = str.Length;
            if (len == 0)
                return str;

            var sb = new StringBuilder(len + 20);
            var replacedCharacter = false;
            for (var n = 0; n < len; ++n)
            {
                var c = str[n];
                if (c != '_' && c != '-' && (char.IsSymbol(c) || char.IsPunctuation(c)))
                {
                    int ascii = c;
                    sb.AppendFormat("{0}", ascii);
                    replacedCharacter = true;
                    continue;
                }
                sb.Append(c);
            }
            if (replacedCharacter)
                str = sb.ToString();

            str = RemoveNonAlphanumerics.Replace(str, string.Empty);
            if (char.IsDigit(str[0]))
                str = "C" + str;

            return str;
        };
    }
}