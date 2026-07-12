using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sql_Compare
{
    public class SqlComparer
    {
        // Quote identifier with backticks (MariaDB/MySQL style)
        private static string Q(string name) => $"`{name}`";
        private static string QI(string name) => $"`{name}`";

        // Format table full name
        private static string Tbl(TableSchema t) => string.IsNullOrEmpty(t.Schema) ? $"`{t.Name}`" : $"`{t.Schema}`.`{t.Name}`";

        public CompareResult Compare(DatabaseSchema local, DatabaseSchema server, string localFileName, string serverFileName)
        {
            var result = new CompareResult
            {
                LocalFileName = localFileName,
                ServerFileName = serverFileName
            };

            var localTables = local.Tables.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
            var serverTables = server.Tables.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

            // Find tables added (in local but not in server)
            foreach (var kvp in localTables)
            {
                if (!serverTables.ContainsKey(kvp.Key))
                {
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.TableAdded,
                        TableName = kvp.Key,
                        ObjectName = kvp.Key,
                        Description = $"Table [{kvp.Key}] exists in Local only",
                        DetailNew = GetTableSummary(kvp.Value),
                        SqlScript = GenerateCreateTableScript(kvp.Value)
                    });
                }
            }

            // Find tables removed (in server but not in local)
            foreach (var kvp in serverTables)
            {
                if (!localTables.ContainsKey(kvp.Key))
                {
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.TableRemoved,
                        TableName = kvp.Key,
                        ObjectName = kvp.Key,
                        Description = $"Table [{kvp.Key}] exists in Server only",
                        DetailOld = GetTableSummary(kvp.Value),
                        SqlScript = GenerateDropTableScript(kvp.Value)
                    });
                }
            }

            // Compare tables that exist in both
            foreach (var kvp in localTables)
            {
                if (!serverTables.TryGetValue(kvp.Key, out var serverTable)) continue;

                var localTable = kvp.Value;
                CompareColumns(localTable, serverTable, result);
                ComparePrimaryKeys(localTable, serverTable, result);
                CompareForeignKeys(localTable, serverTable, result);
                CompareIndexes(localTable, serverTable, result);
                CompareUniqueConstraints(localTable, serverTable, result);
                CompareCheckConstraints(localTable, serverTable, result);
                CompareTableData(localTable, serverTable, result);
            }

            return result;
        }

        private string GetTableSummary(TableSchema table)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Columns ({table.Columns.Count}):");
            foreach (var col in table.Columns)
            {
                sb.AppendLine($"  [{col.Name}] {col.GetDataTypeDisplay()}" +
                    (col.IsNullable ? " NULL" : " NOT NULL") +
                    (col.IsIdentity ? " IDENTITY" : "") +
                    (!string.IsNullOrEmpty(col.DefaultValue) ? $" DEFAULT {col.DefaultValue}" : ""));
            }
            if (table.PrimaryKey != null)
                sb.AppendLine($"PK: {table.PrimaryKey.Name} ({string.Join(", ", table.PrimaryKey.Columns)})");
            foreach (var fk in table.ForeignKeys)
                sb.AppendLine($"FK: {fk.Name} ({string.Join(", ", fk.SourceColumns)}) -> {fk.GetReferencedTableFullName()} ({string.Join(", ", fk.ReferencedColumns)})");
            return sb.ToString();
        }

        private void CompareColumns(TableSchema local, TableSchema server, CompareResult result)
        {
            var localCols = local.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            var serverCols = server.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            // Columns added
            foreach (var kvp in localCols)
            {
                if (!serverCols.ContainsKey(kvp.Key))
                {
                    var col = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.ColumnAdded,
                        TableName = local.Name,
                        ObjectName = col.Name,
                        Description = $"Column [{col.Name}] exists in Local only ([{local.Name}])",
                        DetailNew = $"[{col.Name}] {col.GetDataTypeDisplay()}" +
                            (col.IsNullable ? " NULL" : " NOT NULL") +
                            (col.IsIdentity ? " IDENTITY" : "") +
                            (!string.IsNullOrEmpty(col.DefaultValue) ? $" DEFAULT {col.DefaultValue}" : ""),
                        SqlScript = GenerateAddColumnScript(local, col)
                    });
                }
            }

            // Columns removed
            foreach (var kvp in serverCols)
            {
                if (!localCols.ContainsKey(kvp.Key))
                {
                    var col = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.ColumnRemoved,
                        TableName = local.Name,
                        ObjectName = col.Name,
                        Description = $"Column [{col.Name}] exists in Server only ([{local.Name}])",
                        DetailOld = $"[{col.Name}] {col.GetDataTypeDisplay()}",
                        SqlScript = GenerateDropColumnScript(local, col)
                    });
                }
            }

            // Columns modified
            foreach (var kvp in localCols)
            {
                if (!serverCols.TryGetValue(kvp.Key, out var serverCol)) continue;

                var localCol = kvp.Value;
                var changes = new List<string>();

                if (!string.Equals(localCol.DataType, serverCol.DataType, StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add($"DataType: {serverCol.DataType} -> {localCol.DataType}");
                }
                else if (localCol.MaxLength != serverCol.MaxLength)
                {
                    changes.Add($"MaxLength: {(serverCol.MaxLength.HasValue ? serverCol.MaxLength.ToString() : "MAX")} -> {(localCol.MaxLength.HasValue ? localCol.MaxLength.ToString() : "MAX")}");
                }
                else if (localCol.Precision != serverCol.Precision || localCol.Scale != serverCol.Scale)
                {
                    changes.Add($"Precision/Scale: ({serverCol.Precision},{serverCol.Scale}) -> ({localCol.Precision},{localCol.Scale})");
                }

                if (localCol.IsUnsigned != serverCol.IsUnsigned)
                {
                    changes.Add($"Unsigned: {serverCol.IsUnsigned} -> {localCol.IsUnsigned}");
                }

                if (localCol.IsNullable != serverCol.IsNullable)
                {
                    changes.Add($"Nullable: {serverCol.IsNullable} -> {localCol.IsNullable}");
                }

                if (localCol.IsIdentity != serverCol.IsIdentity)
                {
                    changes.Add($"Identity: {serverCol.IsIdentity} -> {localCol.IsIdentity}");
                }

                if (!string.Equals(localCol.DefaultValue ?? "", serverCol.DefaultValue ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add($"Default: {(string.IsNullOrEmpty(serverCol.DefaultValue) ? "NONE" : serverCol.DefaultValue)} -> {(string.IsNullOrEmpty(localCol.DefaultValue) ? "NONE" : localCol.DefaultValue)}");
                }

                if (changes.Count > 0)
                {
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.ColumnModified,
                        TableName = local.Name,
                        ObjectName = localCol.Name,
                        Description = $"Column [{localCol.Name}] modified in [{local.Name}]",
                        DetailOld = $"[{serverCol.Name}] {serverCol.GetDataTypeDisplay()}" +
                            (serverCol.IsNullable ? " NULL" : " NOT NULL"),
                        DetailNew = $"[{localCol.Name}] {localCol.GetDataTypeDisplay()}" +
                            (localCol.IsNullable ? " NULL" : " NOT NULL"),
                        SqlScript = GenerateAlterColumnScript(local, localCol, serverCol, changes)
                    });
                }
            }
        }

        private void ComparePrimaryKeys(TableSchema local, TableSchema server, CompareResult result)
        {
            var localPk = local.PrimaryKey;
            var serverPk = server.PrimaryKey;

            if (localPk == null && serverPk != null)
            {
                result.Items.Add(new DiffItem
                {
                    Type = DiffType.PrimaryKeyRemoved,
                    TableName = local.Name,
                    ObjectName = serverPk.Name,
                        Description = $"Primary key [{serverPk.Name}] exists in Server only ([{local.Name}])",
                    DetailOld = $"[{serverPk.Name}] on ({string.Join(", ", serverPk.Columns)})",
                    SqlScript = GenerateDropPrimaryKeyScript(local, serverPk)
                });
            }
            else if (localPk != null && serverPk == null)
            {
                result.Items.Add(new DiffItem
                {
                    Type = DiffType.PrimaryKeyAdded,
                    TableName = local.Name,
                    ObjectName = localPk.Name,
                        Description = $"Primary key [{localPk.Name}] exists in Local only ([{local.Name}])",
                    DetailNew = $"[{localPk.Name}] on ({string.Join(", ", localPk.Columns)})",
                    SqlScript = GenerateAddPrimaryKeyScript(local, localPk)
                });
            }
            else if (localPk != null && serverPk != null)
            {
                var colsEqual = localPk.Columns.SequenceEqual(serverPk.Columns, StringComparer.OrdinalIgnoreCase);
                if (!colsEqual || localPk.IsClustered != serverPk.IsClustered)
                {
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.PrimaryKeyModified,
                        TableName = local.Name,
                        ObjectName = localPk.Name,
                        Description = $"Primary key [{localPk.Name}] modified in [{local.Name}]",
                        DetailOld = $"[{serverPk.Name}] on ({string.Join(", ", serverPk.Columns)}) clustered={serverPk.IsClustered}",
                        DetailNew = $"[{localPk.Name}] on ({string.Join(", ", localPk.Columns)}) clustered={localPk.IsClustered}",
                        SqlScript = GenerateAlterPrimaryKeyScript(local, localPk, serverPk)
                    });
                }
            }
        }

        private void CompareForeignKeys(TableSchema local, TableSchema server, CompareResult result)
        {
            var localFks = local.ForeignKeys.ToDictionary(fk => fk.Name, StringComparer.OrdinalIgnoreCase);
            var serverFks = server.ForeignKeys.ToDictionary(fk => fk.Name, StringComparer.OrdinalIgnoreCase);

            // FKs added
            foreach (var kvp in localFks)
            {
                if (!serverFks.ContainsKey(kvp.Key))
                {
                    var fk = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.ForeignKeyAdded,
                        TableName = local.Name,
                        ObjectName = fk.Name,
                        Description = $"Foreign key [{fk.Name}] exists in Local only ([{local.Name}])",
                        DetailNew = $"({string.Join(", ", fk.SourceColumns)}) -> {fk.GetReferencedTableFullName()} ({string.Join(", ", fk.ReferencedColumns)})",
                        SqlScript = GenerateAddForeignKeyScript(local, fk)
                    });
                }
            }

            // FKs removed
            foreach (var kvp in serverFks)
            {
                if (!localFks.ContainsKey(kvp.Key))
                {
                    var fk = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.ForeignKeyRemoved,
                        TableName = local.Name,
                        ObjectName = fk.Name,
                        Description = $"Foreign key [{fk.Name}] exists in Server only ([{local.Name}])",
                        DetailOld = $"({string.Join(", ", fk.SourceColumns)}) -> {fk.GetReferencedTableFullName()} ({string.Join(", ", fk.ReferencedColumns)})",
                        SqlScript = GenerateDropForeignKeyScript(local, fk)
                    });
                }
            }
        }

        private void CompareIndexes(TableSchema local, TableSchema server, CompareResult result)
        {
            var localIdxs = local.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
            var serverIdxs = server.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in localIdxs)
            {
                if (!serverIdxs.ContainsKey(kvp.Key))
                {
                    var idx = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.IndexAdded,
                        TableName = local.Name,
                        ObjectName = idx.Name,
                        Description = $"Index [{idx.Name}] exists in Local only ([{local.Name}])",
                        DetailNew = $"({string.Join(", ", idx.Columns)})" + (idx.IsUnique ? " UNIQUE" : ""),
                        SqlScript = GenerateCreateIndexScript(local, idx)
                    });
                }
            }

            foreach (var kvp in serverIdxs)
            {
                if (!localIdxs.ContainsKey(kvp.Key))
                {
                    var idx = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.IndexRemoved,
                        TableName = local.Name,
                        ObjectName = idx.Name,
                        Description = $"Index [{idx.Name}] exists in Server only ([{local.Name}])",
                        DetailOld = $"({string.Join(", ", idx.Columns)})",
                        SqlScript = GenerateDropIndexScript(local, idx)
                    });
                }
            }
        }

        private void CompareUniqueConstraints(TableSchema local, TableSchema server, CompareResult result)
        {
            var localUqs = local.UniqueConstraints.ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);
            var serverUqs = server.UniqueConstraints.ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in localUqs)
            {
                if (!serverUqs.ContainsKey(kvp.Key))
                {
                    var uq = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.UniqueConstraintAdded,
                        TableName = local.Name,
                        ObjectName = uq.Name,
                        Description = $"Unique constraint [{uq.Name}] exists in Local only ([{local.Name}])",
                        DetailNew = $"({string.Join(", ", uq.Columns)})",
                        SqlScript = GenerateAddUniqueConstraintScript(local, uq)
                    });
                }
            }

            foreach (var kvp in serverUqs)
            {
                if (!localUqs.ContainsKey(kvp.Key))
                {
                    var uq = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.UniqueConstraintRemoved,
                        TableName = local.Name,
                        ObjectName = uq.Name,
                        Description = $"Unique constraint [{uq.Name}] exists in Server only ([{local.Name}])",
                        DetailOld = $"({string.Join(", ", uq.Columns)})",
                        SqlScript = GenerateDropUniqueConstraintScript(local, uq)
                    });
                }
            }
        }

        private void CompareCheckConstraints(TableSchema local, TableSchema server, CompareResult result)
        {
            var localCcs = local.CheckConstraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            var serverCcs = server.CheckConstraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in localCcs)
            {
                if (!serverCcs.ContainsKey(kvp.Key))
                {
                    var cc = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.CheckConstraintAdded,
                        TableName = local.Name,
                        ObjectName = cc.Name,
                        Description = $"Check constraint [{cc.Name}] exists in Local only ([{local.Name}])",
                        DetailNew = cc.Definition,
                        SqlScript = GenerateAddCheckConstraintScript(local, cc)
                    });
                }
            }

            foreach (var kvp in serverCcs)
            {
                if (!localCcs.ContainsKey(kvp.Key))
                {
                    var cc = kvp.Value;
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.CheckConstraintRemoved,
                        TableName = local.Name,
                        ObjectName = cc.Name,
                        Description = $"Check constraint [{cc.Name}] exists in Server only ([{local.Name}])",
                        DetailOld = cc.Definition,
                        SqlScript = GenerateDropCheckConstraintScript(local, cc)
                    });
                }
            }
        }

        private void CompareTableData(TableSchema local, TableSchema server, CompareResult result)
        {
            if (local.Rows.Count == 0 && server.Rows.Count == 0) return;

            // Determine primary key columns for matching
            var pkColumns = local.PrimaryKey?.Columns ?? new List<string>();
            // If no PK, use all columns (fallback)
            var matchColumns = pkColumns.Count > 0 ? pkColumns : local.Columns.Select(c => c.Name).ToList();
            if (matchColumns.Count == 0) return;

            // Build lookup for server rows by the match key
            var serverLookup = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in server.Rows)
            {
                var key = string.Join("|", matchColumns.Select(c => row.ContainsKey(c) ? row[c] : ""));
                if (!serverLookup.ContainsKey(key))
                    serverLookup[key] = row;
            }

            // Compare local rows against server
            var matchedServerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var localRow in local.Rows)
            {
                var key = string.Join("|", matchColumns.Select(c => localRow.ContainsKey(c) ? localRow[c] : ""));

                if (!serverLookup.TryGetValue(key, out var serverRow))
                {
                    // Row exists in local but not in server
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.RowAdded,
                        TableName = local.Name,
                        ObjectName = $"Row {key}",
                        Description = $"Row exists in Local only ([{local.Name}], {FormatRowKey(key, matchColumns)})",
                        DetailNew = FormatRowDetail(localRow),
                        SqlScript = GenerateInsertRowScript(local, localRow)
                    });
                }
                else
                {
                    matchedServerKeys.Add(key);
                    // Check for modified values (excluding key columns)
                    var modifiedCols = new List<string>();
                    foreach (var col in local.Columns.Select(c => c.Name))
                    {
                        if (matchColumns.Contains(col, StringComparer.OrdinalIgnoreCase)) continue;
                        var localVal = localRow.ContainsKey(col) ? localRow[col] : "";
                        var serverVal = serverRow.ContainsKey(col) ? serverRow[col] : "";
                        if (!string.Equals(localVal, serverVal, StringComparison.Ordinal))
                        {
                            modifiedCols.Add($"{col}: {serverVal} -> {localVal}");
                        }
                    }
                    if (modifiedCols.Count > 0)
                    {
                        result.Items.Add(new DiffItem
                        {
                            Type = DiffType.RowModified,
                            TableName = local.Name,
                            ObjectName = $"Row {key}",
                            Description = $"Row modified in [{local.Name}] ({FormatRowKey(key, matchColumns)})",
                            DetailOld = FormatRowDetail(serverRow),
                            DetailNew = FormatRowDetail(localRow),
                            SqlScript = GenerateUpdateRowScript(local, localRow, matchColumns)
                        });
                    }
                }
            }

            // Find rows removed (in server but not in local)
            foreach (var kvp in serverLookup)
            {
                if (!matchedServerKeys.Contains(kvp.Key))
                {
                    result.Items.Add(new DiffItem
                    {
                        Type = DiffType.RowRemoved,
                        TableName = local.Name,
                        ObjectName = $"Row {kvp.Key}",
                        Description = $"Row exists in Server only ([{local.Name}], {FormatRowKey(kvp.Key, matchColumns)})",
                        DetailOld = FormatRowDetail(kvp.Value),
                        SqlScript = GenerateDeleteRowScript(local, kvp.Value, matchColumns)
                    });
                }
            }
        }

        private string FormatRowKey(string key, List<string> matchColumns)
        {
            var parts = key.Split('|');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length && i < matchColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{matchColumns[i]}={parts[i]}");
            }
            return sb.ToString();
        }

        private string FormatRowDetail(Dictionary<string, string> row)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Values:");
            foreach (var kvp in row)
            {
                sb.AppendLine($"  {kvp.Key} = {kvp.Value}");
            }
            return sb.ToString().TrimEnd();
        }

        private string GenerateInsertRowScript(TableSchema table, Dictionary<string, string> row)
        {
            var cols = new List<string>();
            var vals = new List<string>();
            foreach (var col in table.Columns)
            {
                if (row.TryGetValue(col.Name, out var val))
                {
                    cols.Add(Q(col.Name));
                    vals.Add(val); // already properly formatted from the dump
                }
            }
            return $"INSERT INTO {Tbl(table)} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
        }

        private string GenerateDeleteRowScript(TableSchema table, Dictionary<string, string> row, List<string> matchColumns)
        {
            var whereClauses = new List<string>();
            foreach (var col in matchColumns)
            {
                if (row.TryGetValue(col, out var val))
                {
                    whereClauses.Add($"{Q(col)} = {val}");
                }
            }
            return $"DELETE FROM {Tbl(table)} WHERE {string.Join(" AND ", whereClauses)};";
        }

        private string GenerateUpdateRowScript(TableSchema table, Dictionary<string, string> newRow, List<string> keyColumns)
        {
            var setClauses = new List<string>();
            var whereClauses = new List<string>();
            foreach (var col in table.Columns.Select(c => c.Name))
            {
                if (!newRow.ContainsKey(col)) continue;
                if (keyColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
                {
                    whereClauses.Add($"{Q(col)} = {newRow[col]}");
                }
                else
                {
                    setClauses.Add($"{Q(col)} = {newRow[col]}");
                }
            }
            return $"UPDATE {Tbl(table)} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};";
        }

        // ========== MariaDB-compatible script generation methods ==========

        private string GenerateCreateTableScript(TableSchema table)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {Tbl(table)} (");
            var lines = new List<string>();

            foreach (var col in table.Columns)
            {
                var colDef = GenerateColumnDefinition(col);
                lines.Add($"    {colDef}");
            }

            if (table.PrimaryKey != null)
            {
                var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(Q));
                lines.Add($"    PRIMARY KEY ({pkCols})");
            }

            foreach (var fk in table.ForeignKeys)
            {
                var srcCols = string.Join(", ", fk.SourceColumns.Select(Q));
                var refCols = string.Join(", ", fk.ReferencedColumns.Select(Q));
                var refTable = string.IsNullOrEmpty(fk.ReferencedTableSchema) ? Q(fk.ReferencedTable) : $"{Q(fk.ReferencedTableSchema)}.{Q(fk.ReferencedTable)}";
                var fkLine = $"    CONSTRAINT {Q(fk.Name)} FOREIGN KEY ({srcCols}) REFERENCES {refTable} ({refCols})";
                if (!string.IsNullOrEmpty(fk.OnDelete)) fkLine += $" ON DELETE {fk.OnDelete}";
                if (!string.IsNullOrEmpty(fk.OnUpdate)) fkLine += $" ON UPDATE {fk.OnUpdate}";
                lines.Add(fkLine);
            }

            foreach (var uq in table.UniqueConstraints)
            {
                var uqCols = string.Join(", ", uq.Columns.Select(Q));
                lines.Add($"    UNIQUE KEY {Q(uq.Name)} ({uqCols})");
            }

            foreach (var cc in table.CheckConstraints)
            {
                lines.Add($"    CONSTRAINT {Q(cc.Name)} CHECK ({cc.Definition})");
            }

            sb.AppendLine(string.Join(",\n", lines));
            sb.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
            sb.AppendLine();

            foreach (var idx in table.Indexes)
            {
                sb.AppendLine(GenerateCreateIndexScript(table, idx));
            }

            return sb.ToString();
        }

        private string GenerateColumnDefinition(ColumnSchema col)
        {
            var sb = new StringBuilder();
            sb.Append($"{Q(col.Name)} ");

            if (col.IsComputed)
            {
                sb.Append($"AS {col.ComputedDefinition}");
                return sb.ToString();
            }

            sb.Append(col.GetDataTypeDisplay());

            if (col.IsIdentity)
                sb.Append(" AUTO_INCREMENT");

            if (!string.IsNullOrEmpty(col.Collation))
                sb.Append($" COLLATE {col.Collation}");

            if (col.IsRowGuidCol)
                sb.Append(" ROWGUIDCOL");

            if (!string.IsNullOrEmpty(col.GeneratedAlwaysType))
                sb.Append($" GENERATED ALWAYS AS {col.GeneratedAlwaysType}");

            sb.Append(col.IsNullable ? " NULL" : " NOT NULL");

            if (!string.IsNullOrEmpty(col.DefaultValue))
            {
                sb.Append($" DEFAULT {col.DefaultValue}");
            }

            return sb.ToString();
        }

        private string GenerateDropTableScript(TableSchema table)
        {
            return $"DROP TABLE IF EXISTS {Tbl(table)};";
        }

        private string GenerateAddColumnScript(TableSchema table, ColumnSchema col)
        {
            var def = GenerateColumnDefinition(col);
            var sb = new StringBuilder();
            sb.AppendLine($"ALTER TABLE {Tbl(table)} ADD {def};");
            return sb.ToString();
        }

        private string GenerateDropColumnScript(TableSchema table, ColumnSchema col)
        {
            return $"ALTER TABLE {Tbl(table)} DROP COLUMN {Q(col.Name)};";
        }

        private string GenerateAlterColumnScript(TableSchema table, ColumnSchema localCol, ColumnSchema serverCol, List<string> changes)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- WARNING: Column {Q(localCol.Name)} in {Q(table.Name)} has changes:");
            foreach (var change in changes)
            {
                sb.AppendLine($"--   {change}");
            }
            sb.AppendLine($"ALTER TABLE {Tbl(table)} MODIFY COLUMN {Q(localCol.Name)} {localCol.GetDataTypeDisplay()}" +
                (localCol.IsNullable ? " NULL" : " NOT NULL") +
                (!string.IsNullOrEmpty(localCol.DefaultValue) ? $" DEFAULT {localCol.DefaultValue}" : "") + ";");
            sb.AppendLine();

            return sb.ToString();
        }

        private string GenerateDropPrimaryKeyScript(TableSchema table, PrimaryKeySchema pk)
        {
            return $"ALTER TABLE {Tbl(table)} DROP PRIMARY KEY;";
        }

        private string GenerateAddPrimaryKeyScript(TableSchema table, PrimaryKeySchema pk)
        {
            var cols = string.Join(", ", pk.Columns.Select(Q));
            return $"ALTER TABLE {Tbl(table)} ADD PRIMARY KEY ({cols});";
        }

        private string GenerateAlterPrimaryKeyScript(TableSchema table, PrimaryKeySchema localPk, PrimaryKeySchema serverPk)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GenerateDropPrimaryKeyScript(table, serverPk));
            sb.AppendLine(GenerateAddPrimaryKeyScript(table, localPk));
            return sb.ToString();
        }

        private string GenerateAddForeignKeyScript(TableSchema table, ForeignKeySchema fk)
        {
            var srcCols = string.Join(", ", fk.SourceColumns.Select(Q));
            var refCols = string.Join(", ", fk.ReferencedColumns.Select(Q));
            var refTable = string.IsNullOrEmpty(fk.ReferencedTableSchema) ? Q(fk.ReferencedTable) : $"{Q(fk.ReferencedTableSchema)}.{Q(fk.ReferencedTable)}";
            var sql = $"ALTER TABLE {Tbl(table)} ADD CONSTRAINT {Q(fk.Name)} FOREIGN KEY ({srcCols}) " +
                $"REFERENCES {refTable} ({refCols})";
            if (!string.IsNullOrEmpty(fk.OnDelete)) sql += $" ON DELETE {fk.OnDelete}";
            if (!string.IsNullOrEmpty(fk.OnUpdate)) sql += $" ON UPDATE {fk.OnUpdate}";
            sql += ";";
            return sql;
        }

        private string GenerateDropForeignKeyScript(TableSchema table, ForeignKeySchema fk)
        {
            return $"ALTER TABLE {Tbl(table)} DROP FOREIGN KEY {Q(fk.Name)};";
        }

        private string GenerateCreateIndexScript(TableSchema table, IndexSchema idx)
        {
            var cols = string.Join(", ", idx.Columns.Select(c => $"`{c}`"));
            var sql = $"CREATE {(idx.IsUnique ? "UNIQUE " : "")}INDEX {Q(idx.Name)} ON {Tbl(table)} ({cols})";

            if (idx.IncludedColumns.Count > 0)
            {
                sql += $" INCLUDE ({string.Join(", ", idx.IncludedColumns.Select(c => $"`{c}`"))})";
            }

            if (!string.IsNullOrEmpty(idx.Filter))
            {
                sql += $" WHERE {idx.Filter}";
            }

            sql += ";";
            return sql;
        }

        private string GenerateDropIndexScript(TableSchema table, IndexSchema idx)
        {
            return $"ALTER TABLE {Tbl(table)} DROP INDEX {Q(idx.Name)};";
        }

        private string GenerateAddUniqueConstraintScript(TableSchema table, UniqueConstraintSchema uq)
        {
            var cols = string.Join(", ", uq.Columns.Select(Q));
            return $"ALTER TABLE {Tbl(table)} ADD UNIQUE KEY {Q(uq.Name)} ({cols});";
        }

        private string GenerateDropUniqueConstraintScript(TableSchema table, UniqueConstraintSchema uq)
        {
            return $"ALTER TABLE {Tbl(table)} DROP INDEX {Q(uq.Name)};";
        }

        private string GenerateAddCheckConstraintScript(TableSchema table, CheckConstraintSchema cc)
        {
            return $"ALTER TABLE {Tbl(table)} ADD CONSTRAINT {Q(cc.Name)} CHECK ({cc.Definition});";
        }

        private string GenerateDropCheckConstraintScript(TableSchema table, CheckConstraintSchema cc)
        {
            return $"ALTER TABLE {Tbl(table)} DROP CONSTRAINT {Q(cc.Name)};";
        }
    }
}
