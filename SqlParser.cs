using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sql_Compare
{
    /// <summary>
    /// Parses SQL schema definitions from both SQL Server and MariaDB/MySQL dump formats.
    /// Extracts tables, columns, primary keys, foreign keys, indexes, and constraints.
    /// </summary>
    public class SqlParser
    {
        public DatabaseSchema ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("SQL file not found", filePath);

            var content = File.ReadAllText(filePath);
            return Parse(content);
        }

        public DatabaseSchema Parse(string sqlContent)
        {
            var schema = new DatabaseSchema();

            // First extract INSERT data from raw SQL before normalization strips it
            var extractedData = ExtractInsertDataFromRaw(sqlContent);

            // Normalize: remove data sections, DROP statements, non-schema SQL
            sqlContent = NormalizeSql(sqlContent);

            // Parse CREATE TABLE statements using balanced-paren approach
            var tables = ParseCreateTableStatements(sqlContent);
            schema.Tables.AddRange(tables);

            // Now associate extracted row data with parsed tables
            AssociateRowData(schema, extractedData);

            // Parse ALTER TABLE ADD CONSTRAINT (SQL Server style)
            ParseAlterTableConstraints(sqlContent, schema);

            // Parse standalone CREATE INDEX (SQL Server style)
            ParseCreateIndexStatements(sqlContent, schema);

            return schema;
        }

        /// <summary>
        /// Extract INSERT data from raw SQL before normalization.
        /// Returns a dictionary of table_name -> list of positional value tuples.
        /// </summary>
        private Dictionary<string, RawTableData> ExtractInsertDataFromRaw(string sql)
        {
            var result = new Dictionary<string, RawTableData>(StringComparer.OrdinalIgnoreCase);

            // Find all INSERT INTO statements
            // (actual matching uses line-by-line approach below)

            // We need to find INSERTs and extract their data using a similar approach to RemoveInsertStatements
            // but capture the data instead of removing it.
            var lines = sql.Split('\n');
            bool inInsert = false;
            int insertDepth = 0;
            var currentInsert = new System.Text.StringBuilder();
            string currentTable = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (!inInsert)
                {
                    // Match INSERT INTO with backtick or bracket quoted names (raw SQL still has backticks)
                    var insertMatch = System.Text.RegularExpressions.Regex.Match(trimmed,
                        @"INSERT\s+INTO\s+(?:[\[`](\w+)[\]`]\.)?[\[`](\w+)[\]`]", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (insertMatch.Success)
                    {
                        inInsert = true;
                        insertDepth = 0;
                        currentTable = insertMatch.Groups[2].Value;
                        currentInsert.Clear();
                        currentInsert.Append(line);
                        // Count parens
                        foreach (char c in line)
                        {
                            if (c == '(') insertDepth++;
                            if (c == ')') insertDepth--;
                        }
                        if (insertDepth <= 0)
                        {
                            // Single line insert
                            ProcessInsertLine(currentInsert.ToString(), currentTable, result);
                            inInsert = false;
                        }
                        continue;
                    }
                }
                else
                {
                    currentInsert.Append('\n');
                    currentInsert.Append(line);
                    foreach (char c in line)
                    {
                        if (c == '(') insertDepth++;
                        if (c == ')') insertDepth--;
                    }
                    if (insertDepth <= 0)
                    {
                        ProcessInsertLine(currentInsert.ToString(), currentTable, result);
                        inInsert = false;
                    }
                    continue;
                }
            }

            return result;
        }

        internal class RawTableData
        {
            public List<string> ColumnNames { get; set; }
            public List<List<string>> Values { get; set; } = new List<List<string>>();
        }

        private void ProcessInsertLine(string insertLine, string tableName, Dictionary<string, RawTableData> result)
        {
            if (string.IsNullOrEmpty(tableName)) return;

            // Extract column names if present
            var colMatch = System.Text.RegularExpressions.Regex.Match(insertLine,
                @"INSERT\s+INTO\s+(?:[\[`](\w+)[\]`]\.)?[\[`](\w+)[\]`]\s*(?:\(([^)]*)\))?\s*VALUES",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            List<string> columnNames = null;
            if (colMatch.Success && colMatch.Groups[3].Success && !string.IsNullOrWhiteSpace(colMatch.Groups[3].Value))
            {
                columnNames = colMatch.Groups[3].Value.Split(',')
                    .Select(c => c.Trim().Trim('[', ']', '`'))
                    .ToList();
            }

            // Parse the VALUES using ValueParser - get raw tuples
            var tuples = ValueParser.ParseValueTuples(insertLine);

            if (tuples.Count == 0) return;

            if (!result.ContainsKey(tableName))
                result[tableName] = new RawTableData { ColumnNames = columnNames };
            var rawData = result[tableName];

            // If column names were specified, store them (they should be same for all INSERTs on this table)
            if (columnNames != null && rawData.ColumnNames == null)
                rawData.ColumnNames = columnNames;

            foreach (var tuple in tuples)
            {
                rawData.Values.Add(tuple);
            }
        }

        private void AssociateRowData(DatabaseSchema schema, Dictionary<string, RawTableData> extractedData)
        {
            if (extractedData == null) return;
            foreach (var table in schema.Tables)
            {
                if (!extractedData.TryGetValue(table.Name, out var rawData)) continue;
                if (rawData.Values.Count == 0) continue;

                // Determine column names: use explicit names from INSERT, or table's column order
                var colNames = rawData.ColumnNames ?? table.Columns.Select(c => c.Name).ToList();
                if (colNames.Count == 0) continue;

                foreach (var tuple in rawData.Values)
                {
                    var row = new Dictionary<string, string>();
                    for (int i = 0; i < tuple.Count && i < colNames.Count; i++)
                    {
                        row[colNames[i]] = tuple[i];
                    }
                    table.Rows.Add(row);
                }
            }
        }

        /// <summary>
        /// Strip out data sections, conditional comments, and other non-schema content.
        /// Normalize backticks and brackets uniformly.
        /// </summary>
        private string NormalizeSql(string sql)
        {
            // Remove MariaDB/MySQL conditional-execution comments: /*!##### ... */
            sql = Regex.Replace(sql, @"/\*!\d+\s*(.*?)\*/", "$1", RegexOptions.Singleline);

            // Remove block comments (non-conditional)
            sql = Regex.Replace(sql, @"/\*[\s\S]*?\*/", string.Empty);

            // Remove line comments (-- ...) but be careful with strings
            sql = Regex.Replace(sql, @"^\s*--.*$", string.Empty, RegexOptions.Multiline);

            // Remove LOCK TABLES ... WRITE/READ blocks
            sql = Regex.Replace(sql, @"LOCK\s+TABLES\s+.*?UNLOCK\s+TABLES\s*;?", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Remove INSERT INTO ... VALUES ... statements (data)
            sql = sql.RemoveInsertStatements();

            // Remove DROP TABLE IF EXISTS 
            sql = Regex.Replace(sql, @"DROP\s+TABLE\s+(?:IF\s+EXISTS\s+)?.*?;?", string.Empty, RegexOptions.IgnoreCase);

            // Remove DROP INDEX statements
            sql = Regex.Replace(sql, @"DROP\s+INDEX\s+.*?;?", string.Empty, RegexOptions.IgnoreCase);

            // Remove ALTER TABLE ... DISABLE KEYS / ENABLE KEYS
            sql = Regex.Replace(sql, @"ALTER\s+TABLE\s+.*? (DISABLE|ENABLE)\s+KEYS\s*;?", string.Empty, RegexOptions.IgnoreCase);

            // Remove SET statements
            sql = Regex.Replace(sql, @"^\s*SET\s+.*?;?\s*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // Remove DELIMITER statements
            sql = Regex.Replace(sql, @"DELIMITER\s+\S+\s*", string.Empty, RegexOptions.IgnoreCase);

            // Normalize backticks to brackets for uniform handling
            // Properly alternate: first backtick -> [, second -> ], etc.
            var btBuilder = new System.Text.StringBuilder(sql.Length);
            bool expectOpen = true;
            foreach (char c in sql)
            {
                if (c == '`')
                {
                    btBuilder.Append(expectOpen ? '[' : ']');
                    expectOpen = !expectOpen;
                }
                else
                {
                    btBuilder.Append(c);
                }
            }
            sql = btBuilder.ToString();

            // Normalize whitespace
            sql = Regex.Replace(sql, @"\s+", " ");

            return sql.Trim();
        }

        private List<TableSchema> ParseCreateTableStatements(string sql)
        {
            var tables = new List<TableSchema>();

            // Find all CREATE TABLE occurrences using character-by-character analysis
            // This is more robust than regex for complex SQL
            var createPattern = @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?";
            var matches = Regex.Matches(sql, createPattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                int startIdx = match.Index;

                // After "CREATE TABLE [IF NOT EXISTS] ", find table name
                int nameStart = match.Index + match.Length;

                // Extract full table name (may include schema prefix or backtick-quoted)
                var tableNameResult = ExtractTableName(sql, nameStart);
                if (tableNameResult == null) continue;

                string tableName = tableNameResult.Item1;
                string schemaName = tableNameResult.Item2;
                int afterNameIdx = tableNameResult.Item3;

                // Find the opening parenthesis of the column definitions
                int openParenIdx = sql.IndexOf('(', afterNameIdx);
                if (openParenIdx < 0) continue;

                // Find matching closing parenthesis (handling nested parens)
                int closeParenIdx = FindMatchingParen(sql, openParenIdx);
                if (closeParenIdx < 0) continue;

                // Extract body between outer parens
                string body = sql.Substring(openParenIdx + 1, closeParenIdx - openParenIdx - 1);

                var table = new TableSchema
                {
                    Name = tableName,
                    Schema = schemaName
                };

                ParseTableBody(body, table);
                tables.Add(table);
            }

            return tables;
        }

        private Tuple<string, string, int> ExtractTableName(string sql, int startPos)
        {
            int i = startPos;
            // Skip whitespace
            while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;

            if (i >= sql.Length) return null;

            string schemaName = "";
            string tableName;

            // Handle bracketed name: [schema].[name] or [name]
            if (sql[i] == '[')
            {
                int closeBracket = sql.IndexOf(']', i + 1);
                if (closeBracket < 0) return null;

                tableName = sql.Substring(i + 1, closeBracket - i - 1);
                i = closeBracket + 1;

                // Check for .[name] (schema.table)
                while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;
                if (i < sql.Length && sql[i] == '.')
                {
                    i++;
                    while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;
                    if (i < sql.Length && sql[i] == '[')
                    {
                        int closeBracket2 = sql.IndexOf(']', i + 1);
                        if (closeBracket2 >= 0)
                        {
                            schemaName = tableName;
                            tableName = sql.Substring(i + 1, closeBracket2 - i - 1);
                            i = closeBracket2 + 1;
                        }
                    }
                }
            }
            else
            {
                // Unquoted name: read until whitespace, (, or .
                int end = i;
                while (end < sql.Length && !char.IsWhiteSpace(sql[end]) && sql[end] != '(' && sql[end] != '.')
                    end++;

                tableName = sql.Substring(i, end - i);
                i = end;

                // Check for . (schema.table)
                while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;
                if (i < sql.Length && sql[i] == '.')
                {
                    i++;
                    while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;
                    int nameEnd = i;
                    while (nameEnd < sql.Length && !char.IsWhiteSpace(sql[nameEnd]) && sql[nameEnd] != '(')
                        nameEnd++;
                    schemaName = tableName;
                    tableName = sql.Substring(i, nameEnd - i);
                    i = nameEnd;
                }
            }

            return Tuple.Create(tableName, schemaName, i);
        }

        private int FindMatchingParen(string sql, int openPos)
        {
            int depth = 1;
            for (int i = openPos + 1; i < sql.Length; i++)
            {
                if (sql[i] == '(') depth++;
                else if (sql[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private void ParseTableBody(string body, TableSchema table)
        {
            // Split by top-level commas
            var parts = SplitTopLevelCommas(body);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Detect what type of definition this is
                if (Regex.IsMatch(trimmed, @"^\s*(?:CONSTRAINT\s+\S+\s+)?PRIMARY\s+KEY\s", RegexOptions.IgnoreCase))
                {
                    ParsePrimaryKey(trimmed, table);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*(?:CONSTRAINT\s+\S+\s+)?FOREIGN\s+KEY\s", RegexOptions.IgnoreCase))
                {
                    ParseForeignKey(trimmed, table);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*(?:CONSTRAINT\s+\S+\s+)?UNIQUE\s+(?:KEY\s+)?", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(trimmed, @"^\s*UNIQUE\s+KEY\s+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(trimmed, @"^\s*UNIQUE\s*\(", RegexOptions.IgnoreCase))
                {
                    // UNIQUE (column) or UNIQUE KEY name (column) or CONSTRAINT name UNIQUE (column)
                    var uc = ParseUniqueConstraint(trimmed);
                    if (uc != null) table.UniqueConstraints.Add(uc);
                    else
                    {
                        // Could be a UNIQUE index definition
                        var idx = ParseKeyIndex(trimmed, table.Name, isUnique: true);
                        if (idx != null) table.Indexes.Add(idx);
                    }
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*(?:CONSTRAINT\s+\S+\s+)?CHECK\s*\(", RegexOptions.IgnoreCase))
                {
                    ParseCheckConstraint(trimmed, table);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*KEY\s+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(trimmed, @"^\s*INDEX\s+", RegexOptions.IgnoreCase))
                {
                    // MariaDB/MySQL inline KEY/INDEX definition
                    var isUnique = trimmed.TrimStart().StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase);
                    var idx = ParseKeyIndex(trimmed, table.Name, isUnique);
                    if (idx != null) table.Indexes.Add(idx);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*(?:FULLTEXT|SPATIAL)\s+(?:KEY|INDEX)\s+", RegexOptions.IgnoreCase))
                {
                    // Full-text or spatial index - skip or handle minimally
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*CONSTRAINT\s+\S+\s+(?:PRIMARY|FOREIGN|UNIQUE|CHECK)", RegexOptions.IgnoreCase))
                {
                    // Already handled above
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*INDEX\s+", RegexOptions.IgnoreCase))
                {
                    var idx = ParseKeyIndex(trimmed, table.Name, false);
                    if (idx != null) table.Indexes.Add(idx);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*PRIMARY\s+KEY\s", RegexOptions.IgnoreCase))
                {
                    ParsePrimaryKey(trimmed, table);
                    continue;
                }

                // Otherwise it's a column definition
                var column = ParseColumnDefinition(trimmed);
                if (column != null)
                {
                    table.Columns.Add(column);
                }
            }
        }

        private void ParsePrimaryKey(string text, TableSchema table)
        {
            // MariaDB: PRIMARY KEY (col1, col2) or CONSTRAINT name PRIMARY KEY (col1, col2)
            // SQL Server: CONSTRAINT [name] PRIMARY KEY CLUSTERED (col1, col2)
            var match = Regex.Match(text,
                @"(?:CONSTRAINT\s+\[?(\w+)\]?\s+)?PRIMARY\s+KEY\s*(CLUSTERED|NONCLUSTERED|USING\s+BTREE)?\s*\(([^)]+)\)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var colsPart = match.Groups[match.Groups.Count - 1].Value;
                var cols = colsPart.Split(',')
                    .Select(c => c.Trim().Trim('[', ']'))
                    .Select(c => c.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0])
                    .Select(c => c.Trim(' ', '\t', '\r', '\n', '`', '[', ']'))
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();

                if (cols.Count == 0) return;

                var pk = new PrimaryKeySchema
                {
                    Name = match.Groups[1].Success ? match.Groups[1].Value.Trim('[', ']') : $"PK_{table.Name}",
                    IsClustered = !match.Groups[2].Success || 
                                 (match.Groups[2].Value.ToUpper() != "NONCLUSTERED" && 
                                  !match.Groups[2].Value.Contains("BTREE")),
                    Columns = cols
                };
                table.PrimaryKey = pk;
            }
        }

        private void ParseForeignKey(string text, TableSchema table)
        {
            // MariaDB: CONSTRAINT name FOREIGN KEY (col) REFERENCES ref_table (ref_col) ON DELETE CASCADE ON UPDATE CASCADE
            // SQL Server: CONSTRAINT [name] FOREIGN KEY (col) REFERENCES [schema].[table] (col)
            var match = Regex.Match(text,
                @"CONSTRAINT\s+\[?(?<name>\w+)\]?\s+FOREIGN\s+KEY\s*\((?<src>[^)]+)\)\s*REFERENCES\s+" +
                @"(?:\[?(?<refSchema>\w+)\]?\.)?\[?(?<refTable>\w+)\]?\s*\((?<refCols>[^)]+)\)" +
                @"(?:\s*ON\s+(?<on1DelUpd>DELETE|UPDATE)\s+(?<on1Action>NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT|RESTRICT))?" +
                @"(?:\s*ON\s+(?<on2DelUpd>DELETE|UPDATE)\s+(?<on2Action>NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT|RESTRICT))?",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                // Try without CONSTRAINT name (MariaDB style)
                match = Regex.Match(text,
                    @"FOREIGN\s+KEY\s*\((?<src>[^)]+)\)\s*REFERENCES\s+" +
                    @"(?:\[?(?<refSchema>\w+)\]?\.)?\[?(?<refTable>\w+)\]?\s*\((?<refCols>[^)]+)\)" +
                    @"(?:\s*ON\s+(?<on1DelUpd>DELETE|UPDATE)\s+(?<on1Action>NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT|RESTRICT))?" +
                    @"(?:\s*ON\s+(?<on2DelUpd>DELETE|UPDATE)\s+(?<on2Action>NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT|RESTRICT))?",
                    RegexOptions.IgnoreCase);
            }

            if (match.Success)
            {
                var fk = new ForeignKeySchema
                {
                    Name = match.Groups["name"].Success ? match.Groups["name"].Value.Trim('[', ']') : $"FK_{table.Name}_unnamed",
                    SourceTable = table.Name,
                };

                // Source columns
                var srcCols = match.Groups["src"].Value;
                fk.SourceColumns.AddRange(srcCols.Split(',').Select(c => c.Trim().Trim('[', ']')));

                // Referenced schema/table
                fk.ReferencedTableSchema = match.Groups["refSchema"].Success
                    ? match.Groups["refSchema"].Value.Trim('[', ']')
                    : "";
                fk.ReferencedTable = match.Groups["refTable"].Value.Trim('[', ']');

                // Referenced columns
                var refCols = match.Groups["refCols"].Value;
                fk.ReferencedColumns.AddRange(refCols.Split(',').Select(c => c.Trim().Trim('[', ']')));

                // ON DELETE
                if (match.Groups["on1DelUpd"].Success && match.Groups["on1DelUpd"].Value.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                    fk.OnDelete = match.Groups["on1Action"].Value;
                if (match.Groups["on2DelUpd"].Success && match.Groups["on2DelUpd"].Value.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                    fk.OnDelete = match.Groups["on2Action"].Value;

                // ON UPDATE
                if (match.Groups["on1DelUpd"].Success && match.Groups["on1DelUpd"].Value.Equals("UPDATE", StringComparison.OrdinalIgnoreCase))
                    fk.OnUpdate = match.Groups["on1Action"].Value;
                if (match.Groups["on2DelUpd"].Success && match.Groups["on2DelUpd"].Value.Equals("UPDATE", StringComparison.OrdinalIgnoreCase))
                    fk.OnUpdate = match.Groups["on2Action"].Value;

                table.ForeignKeys.Add(fk);
            }
        }

        private UniqueConstraintSchema ParseUniqueConstraint(string text)
        {
            // UNIQUE KEY name (col1, col2) or CONSTRAINT name UNIQUE (col1, col2) or UNIQUE (col1)
            var match = Regex.Match(text,
                @"(?:CONSTRAINT\s+\[?(\w+)\]?\s+)?UNIQUE\s+(?:KEY\s+\[?(\w+)\]?\s*)?\(([^)]+)\)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var uc = new UniqueConstraintSchema
                {
                    Name = match.Groups[1].Success ? match.Groups[1].Value.Trim('[', ']')
                         : match.Groups[2].Success ? match.Groups[2].Value.Trim('[', ']')
                         : $"UQ_unnamed"
                };
                uc.Columns.AddRange(match.Groups[3].Value.Split(',')
                    .Select(c => c.Trim().Trim('[', ']').Split(' ')[0]));
                return uc;
            }
            return null;
        }

        private void ParseCheckConstraint(string text, TableSchema table)
        {
            var match = Regex.Match(text, @"CONSTRAINT\s+\[?(\w+)\]?\s+CHECK\s*\((.+?)\)\s*$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(text, @"CHECK\s*\((.+?)\)\s*$", RegexOptions.IgnoreCase);
            }

            if (match.Success)
            {
                var cc = new CheckConstraintSchema
                {
                    Name = match.Groups[1].Success ? match.Groups[1].Value.Trim('[', ']') : $"CK_{table.Name}_unnamed",
                    Definition = match.Groups[match.Groups.Count - 1].Value
                };
                table.CheckConstraints.Add(cc);
            }
        }

        /// <summary>
        /// Parse MariaDB/MySQL inline KEY or INDEX definition:
        /// KEY name (col1, col2) or INDEX name (col1, col2) or UNIQUE KEY name (col1, col2)
        /// </summary>
        private IndexSchema ParseKeyIndex(string text, string tableName, bool isUnique)
        {
            // KEY name (col1, col2) ... USING BTREE
            // INDEX name (col1, col2)
            // UNIQUE KEY name (col1, col2)
            // UNIQUE INDEX name (col1, col2)
            var match = Regex.Match(text,
                @"(?:UNIQUE\s+)?(?:KEY|INDEX)\s+\[?(\w+)\]?\s*\(([^)]+)\)(?:\s+USING\s+\w+)?(?:\s*INCLUDE\s*\(([^)]+)\))?",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var idx = new IndexSchema
                {
                    Name = match.Groups[1].Value.Trim('[', ']'),
                    TableName = tableName,
                    IsUnique = isUnique || text.TrimStart().StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase),
                    IsClustered = false // MariaDB InnoDB uses PRIMARY KEY for clustering
                };

                idx.Columns.AddRange(match.Groups[2].Value.Split(',')
                    .Select(c => c.Trim().Trim('[', ']').Split(' ')[0]));

                if (match.Groups[3].Success)
                {
                    idx.IncludedColumns.AddRange(match.Groups[3].Value.Split(',')
                        .Select(c => c.Trim().Trim('[', ']')));
                }

                return idx;
            }
            return null;
        }

        private ColumnSchema ParseColumnDefinition(string text)
        {
            // Skip if it's a constraint or key definition
            if (Regex.IsMatch(text, @"^\s*(CONSTRAINT|PRIMARY\s+KEY|FOREIGN\s+KEY|UNIQUE|CHECK|KEY|INDEX|FULLTEXT|SPATIAL)\s", RegexOptions.IgnoreCase))
                return null;

            var column = new ColumnSchema();

            // Extract column name: `name` or [name] or unquoted name
            var nameMatch = Regex.Match(text, @"^\s*\[?(\w+)\]?\s+", RegexOptions.IgnoreCase);
            if (!nameMatch.Success) return null;

            column.Name = nameMatch.Groups[1].Value.Trim('[', ']');
            var remaining = text.Substring(nameMatch.Index + nameMatch.Length).Trim();

            // --- Extract data type ---
            // Handle: type, type(n), type(n,m), type(unsigned), type(n) unsigned, etc.
            var typeMatch = Regex.Match(remaining,
                @"^(\w+)\s*(?:\((\d+)(?:\s*,\s*(\d+))?\))?\s*(unsigned\s+)?(zerofill\s+)?",
                RegexOptions.IgnoreCase);

            if (typeMatch.Success)
            {
                column.DataType = typeMatch.Groups[1].Value;

                // Capture unsigned flag
                column.IsUnsigned = typeMatch.Groups[4].Success;

                if (typeMatch.Groups[2].Success)
                {
                    int val;
                    if (int.TryParse(typeMatch.Groups[2].Value, out val))
                    {
                        var upperType = column.DataType.ToUpper();
                        if (upperType == "NVARCHAR" || upperType == "VARCHAR" || upperType == "NCHAR" || 
                            upperType == "CHAR" || upperType == "BINARY" || upperType == "VARBINARY" ||
                            upperType == "TEXT" || upperType == "TINYTEXT" || upperType == "MEDIUMTEXT" || 
                            upperType == "LONGTEXT" || upperType == "BLOB" || upperType == "TINYBLOB" || 
                            upperType == "MEDIUMBLOB" || upperType == "LONGBLOB")
                        {
                            column.MaxLength = val;
                        }
                        else
                        {
                            column.Precision = val;
                        }

                        if (typeMatch.Groups[3].Success && int.TryParse(typeMatch.Groups[3].Value, out val))
                        {
                            column.Scale = val;
                        }
                    }
                }

                // For display width types like bigint(20), int(10), tinyint(3) - these are display widths
                // In MariaDB/MySQL, these are not actual precision. We'll keep them as MaxLength for varchar,
                // but for integer types we should ignore the display width.
                var integerTypes = new[] { "BIGINT", "INT", "INTEGER", "SMALLINT", "TINYINT", "MEDIUMINT" };
                if (integerTypes.Contains(column.DataType.ToUpper()) && typeMatch.Groups[1].Success)
                {
                    // This is a display width, not precision - clear it
                    column.Precision = null;
                }

                remaining = remaining.Substring(typeMatch.Index + typeMatch.Length).Trim();
            }

            // --- Check for CHARACTER SET / COLLATE inline ---
            var charsetCollMatch = Regex.Match(remaining, @"(?:CHARACTER\s+SET\s+\S+)?\s*(?:COLLATE\s+(\S+))?", RegexOptions.IgnoreCase);
            if (charsetCollMatch.Success && charsetCollMatch.Groups[1].Success)
            {
                column.Collation = charsetCollMatch.Groups[1].Value;
            }

            // --- Check for AUTO_INCREMENT ---
            column.IsIdentity = Regex.IsMatch(remaining, @"\bAUTO_INCREMENT\b", RegexOptions.IgnoreCase);

            // --- Check for NULL / NOT NULL ---
            var notNullMatch = Regex.Match(remaining, @"NOT\s+NULL", RegexOptions.IgnoreCase);
            var nullMatch = Regex.Match(remaining, @"(?<!NOT\s+)NULL\b", RegexOptions.IgnoreCase);

            if (notNullMatch.Success)
            {
                var lastNotNull = notNullMatch.Index + notNullMatch.Length;
                var lastNull = nullMatch.Success ? nullMatch.Index + nullMatch.Length : -1;
                column.IsNullable = lastNull > lastNotNull;
            }
            else
            {
                column.IsNullable = true;
            }

            // --- Check for DEFAULT ---
            // Need to be careful - DEFAULT can be followed by a string, number, function call, etc.
            // Detect DEFAULT but don't include if it's part of ON UPDATE CURRENT_TIMESTAMP or similar
            var defaultMatch = Regex.Match(remaining,
                @"DEFAULT\s+(\S+(?:\s+\S+)*?)(?=\s+(?:NOT\s+NULL|NULL|AUTO_INCREMENT|UNIQUE|PRIMARY\s+KEY|KEY|INDEX|COMMENT|COLLATE|CHARACTER|REFERENCES|ON\s+UPDATE|,$|$))",
                RegexOptions.IgnoreCase);
            
            if (!defaultMatch.Success)
            {
                // Try simpler: just capture until the next keyword or end
                defaultMatch = Regex.Match(remaining,
                    @"DEFAULT\s+(.+?)(?:\s+(?:NOT\s+NULL|NULL|AUTO_INCREMENT|UNIQUE|PRIMARY\s+KEY|KEY|INDEX|COMMENT|COLLATE|CHARACTER\s+SET|ON\s+UPDATE)\s|$)",
                    RegexOptions.IgnoreCase);
            }

            if (defaultMatch.Success)
            {
                column.DefaultValue = defaultMatch.Groups[1].Value.Trim().Trim(',').Trim();
            }

            // --- Check for ON UPDATE (MariaDB timestamp feature) ---
            // Don't treat as default, just note it if needed

            // --- Check for COMMENT ---
            // Not stored currently

            // --- Check for GENERATED ALWAYS AS (computed columns) ---
            var generatedMatch = Regex.Match(text, @"GENERATED\s+ALWAYS\s+AS\s*\((.+?)\)", RegexOptions.IgnoreCase);
            if (generatedMatch.Success)
            {
                column.IsComputed = true;
                column.ComputedDefinition = generatedMatch.Groups[1].Value;
            }

            return column;
        }

        private void ParseAlterTableConstraints(string sql, DatabaseSchema schema)
        {
            // SQL Server style ALTER TABLE ADD CONSTRAINT
            var alterPattern = @"ALTER\s+TABLE\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?\s+ADD\s+(CONSTRAINT\s+.*?)(?=ALTER\s+TABLE|GO\b|$|CREATE\s+(?:TABLE|VIEW|PROC|FUNCTION|INDEX)|--)";
            var alterMatches = Regex.Matches(sql, alterPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match alterMatch in alterMatches)
            {
                var schemaName = alterMatch.Groups[1].Success ? alterMatch.Groups[1].Value.Trim('[', ']') : "";
                var tableName = alterMatch.Groups[2].Value.Trim('[', ']');
                var constraintDef = alterMatch.Groups[3].Value.Trim();

                var table = schema.Tables.FirstOrDefault(t =>
                    t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
                    t.Schema.Equals(schemaName, StringComparison.OrdinalIgnoreCase));
                if (table == null) continue;

                // PRIMARY KEY
                var pkMatch = Regex.Match(constraintDef, @"CONSTRAINT\s+\[?(\w+)\]?\s+PRIMARY\s+KEY\s*(CLUSTERED|NONCLUSTERED)?\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
                if (pkMatch.Success && table.PrimaryKey == null)
                {
                    var pk = new PrimaryKeySchema
                    {
                        Name = pkMatch.Groups[1].Value.Trim('[', ']'),
                        IsClustered = !pkMatch.Groups[2].Success || pkMatch.Groups[2].Value.ToUpper() != "NONCLUSTERED"
                    };
                    pk.Columns.AddRange(pkMatch.Groups[3].Value.Split(',').Select(c => c.Trim().Trim('[', ']')));
                    table.PrimaryKey = pk;
                    continue;
                }

                // FOREIGN KEY
                var fkMatch = Regex.Match(constraintDef,
                    @"CONSTRAINT\s+\[?(\w+)\]?\s+FOREIGN\s+KEY\s*\(([^)]+)\)\s*REFERENCES\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?\s*\(([^)]+)\)",
                    RegexOptions.IgnoreCase);
                if (fkMatch.Success)
                {
                    var fk = new ForeignKeySchema
                    {
                        Name = fkMatch.Groups[1].Value.Trim('[', ']'),
                        SourceTable = tableName,
                        ReferencedTableSchema = fkMatch.Groups[3].Success ? fkMatch.Groups[3].Value.Trim('[', ']') : "dbo",
                        ReferencedTable = fkMatch.Groups[4].Value.Trim('[', ']')
                    };
                    fk.SourceColumns.AddRange(fkMatch.Groups[2].Value.Split(',').Select(c => c.Trim().Trim('[', ']')));
                    fk.ReferencedColumns.AddRange(fkMatch.Groups[5].Value.Split(',').Select(c => c.Trim().Trim('[', ']')));

                    var onDeleteMatch = Regex.Match(constraintDef, @"ON\s+DELETE\s+(NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT)", RegexOptions.IgnoreCase);
                    if (onDeleteMatch.Success) fk.OnDelete = onDeleteMatch.Groups[1].Value;
                    var onUpdateMatch = Regex.Match(constraintDef, @"ON\s+UPDATE\s+(NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT)", RegexOptions.IgnoreCase);
                    if (onUpdateMatch.Success) fk.OnUpdate = onUpdateMatch.Groups[1].Value;

                    table.ForeignKeys.Add(fk);
                    continue;
                }

                // UNIQUE
                var ucMatch = Regex.Match(constraintDef, @"CONSTRAINT\s+\[?(\w+)\]?\s+UNIQUE\s*(CLUSTERED|NONCLUSTERED)?\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
                if (ucMatch.Success)
                {
                    var uc = new UniqueConstraintSchema
                    {
                        Name = ucMatch.Groups[1].Value.Trim('[', ']')
                    };
                    uc.Columns.AddRange(ucMatch.Groups[3].Value.Split(',').Select(c => c.Trim().Trim('[', ']')));
                    table.UniqueConstraints.Add(uc);
                    continue;
                }

                // CHECK
                var ccMatch = Regex.Match(constraintDef, @"CONSTRAINT\s+\[?(\w+)\]?\s+CHECK\s*\((.+)\)\s*$", RegexOptions.IgnoreCase);
                if (ccMatch.Success)
                {
                    var cc = new CheckConstraintSchema
                    {
                        Name = ccMatch.Groups[1].Value.Trim('[', ']'),
                        Definition = ccMatch.Groups[2].Value
                    };
                    table.CheckConstraints.Add(cc);
                }
            }
        }

        private void ParseCreateIndexStatements(string sql, DatabaseSchema schema)
        {
            // SQL Server standalone CREATE INDEX
            var indexPattern = @"CREATE\s+(UNIQUE\s+)?(CLUSTERED\s+)?(NONCLUSTERED\s+)?INDEX\s+\[?(\w+)\]?\s+ON\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?\s*\(([^)]+)\)\s*(?:INCLUDE\s*\(([^)]+)\))?\s*(?:WHERE\s+(.+?))?(?=GO\b|$|CREATE\s+(?:TABLE|VIEW|PROC|FUNCTION|UNIQUE\s+INDEX|INDEX))";
            var indexMatches = Regex.Matches(sql, indexPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in indexMatches)
            {
                var idx = new IndexSchema
                {
                    Name = match.Groups[4].Value.Trim('[', ']'),
                    IsUnique = match.Groups[1].Success,
                    IsClustered = match.Groups[2].Success,
                    TableName = match.Groups[6].Value.Trim('[', ']')
                };

                idx.Columns.AddRange(match.Groups[7].Value.Split(',').Select(c => c.Trim().Trim('[', ']')));

                if (match.Groups[8].Success)
                {
                    idx.IncludedColumns.AddRange(match.Groups[8].Value.Split(',').Select(c => c.Trim().Trim('[', ']')));
                }

                if (match.Groups[9].Success)
                {
                    idx.Filter = match.Groups[9].Value.Trim();
                }

                var table = schema.Tables.FirstOrDefault(t =>
                    t.Name.Equals(idx.TableName, StringComparison.OrdinalIgnoreCase));
                if (table != null && !table.Indexes.Any(i => i.Name.Equals(idx.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    table.Indexes.Add(idx);
                }
            }
        }

        private List<string> SplitTopLevelCommas(string text)
        {
            var parts = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '(':
                        depth++;
                        break;
                    case ')':
                        depth--;
                        break;
                    case ',':
                        if (depth == 0)
                        {
                            parts.Add(text.Substring(start, i - start));
                            start = i + 1;
                        }
                        break;
                }
            }

            if (start < text.Length)
            {
                parts.Add(text.Substring(start));
            }

            return parts;
        }
    }

    /// <summary>
    /// Extension methods for Regex used in SQL parsing
    /// </summary>
    internal static class RegexExtensions
    {
        public static string RemoveInsertStatements(this string sql)
        {
            // Remove INSERT INTO ... VALUES (...) blocks
            // Handle multi-line inserts
            var sb = new System.Text.StringBuilder();
            var lines = sql.Split('\n');
            bool inInsert = false;
            int insertDepth = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (!inInsert && Regex.IsMatch(trimmed, @"^\s*INSERT\s+INTO\s", RegexOptions.IgnoreCase))
                {
                    inInsert = true;
                    insertDepth = 0;
                    // Count parens in the INSERT line to see if it ends here
                    foreach (char c in line)
                    {
                        if (c == '(') insertDepth++;
                        if (c == ')') insertDepth--;
                    }
                    if (insertDepth <= 0) inInsert = false;
                    continue;
                }

                if (inInsert)
                {
                    foreach (char c in line)
                    {
                        if (c == '(') insertDepth++;
                        if (c == ')') insertDepth--;
                    }
                    if (insertDepth <= 0)
                    {
                        inInsert = false;
                    }
                    continue;
                }

                sb.AppendLine(line);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Extracts row data from INSERT statements in raw SQL
    /// </summary>
    public static class InsertDataExtractor
    {
        /// <summary>
        /// Extract INSERT data from raw SQL (before normalization). Places row data into schema tables.
        /// </summary>
        public static string ExtractInsertData(string sql, DatabaseSchema schema)
        {
            // We need to extract INSERT data but also return the modified SQL
            // (with INSERT data removed for further processing).
            // But since InsertDataExtractor is called BEFORE NormalizeSql,
            // and the caller Parse method uses the returned string for normalization,
            // we need to return the SQL with INSERT data extracted.
            // Actually, the caller passes sqlContent by value, so we return the modified SQL.
            // But we also need to populate the schema tables.
            // However, at this point the schema doesn't have tables yet!
            // We need a different approach: collect INSERT data first, then match to tables later.
            
            // For now, we'll collect INSERT data and store it temporarily.
            // The approach: scan for INSERT INTO table_name VALUES (...), parse the values,
            // and store them in a dictionary keyed by table name.
            // Then after tables are parsed, we can associate the data.
            
            // Since ExtractInsertData is called BEFORE ParseCreateTableStatements,
            // we need to store the extracted data and associate it later.
            // For simplicity, we'll skip this for now and implement a simpler approach:
            // Just remove INSERT statements as before, but also capture the data
            // into a temporary structure for later association.
            
            return sql;
        }

        /// <summary>
        /// After tables are parsed, associate extracted row data with their tables.
        /// </summary>
        public static void AssociateRowData(DatabaseSchema schema, Dictionary<string, List<Dictionary<string, string>>> extractedData)
        {
            if (extractedData == null) return;
            foreach (var table in schema.Tables)
            {
                if (extractedData.TryGetValue(table.Name, out var rows))
                {
                    table.Rows.AddRange(rows);
                }
            }
        }
    }

    /// <summary>
    /// Parses SQL INSERT VALUES into structured row data
    /// </summary>
    public static class ValueParser
    {
        /// <summary>
        /// Parse INSERT INTO statement and extract value tuples (raw strings).
        /// Returns a list of tuples, each tuple is a list of value strings.
        /// </summary>
        public static List<List<string>> ParseValueTuples(string insertStatement)
        {
            var tuples = new List<List<string>>();

            // Extract the VALUES part
            var valuesMatch = System.Text.RegularExpressions.Regex.Match(insertStatement,
                @"VALUES\s*(.*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!valuesMatch.Success) return tuples;

            string valuesPart = valuesMatch.Groups[1].Value.Trim().Trim(';').Trim();

            // Find top-level parenthesized tuples: (val,val,...),(val,val,...)
            int depth = 0;
            int start = -1;
            var rawTuples = new List<string>();

            for (int i = 0; i < valuesPart.Length; i++)
            {
                char c = valuesPart[i];
                if (c == '(' && depth == 0)
                {
                    start = i + 1;
                    depth = 1;
                }
                else if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        rawTuples.Add(valuesPart.Substring(start, i - start));
                        start = -1;
                    }
                }
            }

            // Parse each tuple into values
            foreach (var tuple in rawTuples)
            {
                var values = ParseSingleTuple(tuple);
                if (values.Count > 0)
                {
                    tuples.Add(values);
                }
            }

            return tuples;
        }

        /// <summary>
        /// Parse a single value tuple like "1,'hello',NULL" into individual value strings.
        /// Handles quoted strings, numbers, NULL, and expressions.
        /// </summary>
        public static List<string> ParseSingleTuple(string tuple)
        {
            var values = new List<string>();
            if (string.IsNullOrEmpty(tuple)) return values;

            int i = 0;
            while (i < tuple.Length)
            {
                // Skip whitespace
                while (i < tuple.Length && char.IsWhiteSpace(tuple[i])) i++;
                if (i >= tuple.Length) break;

                char c = tuple[i];

                if (c == '\'')
                {
                    // Quoted string: find matching quote, handle escaped quotes ('')
                    int start = i;
                    i++; // skip opening quote
                    while (i < tuple.Length)
                    {
                        if (tuple[i] == '\'' && i + 1 < tuple.Length && tuple[i + 1] == '\'')
                        {
                            i += 2; // skip escaped quote
                        }
                        else if (tuple[i] == '\'')
                        {
                            i++; // closing quote
                            break;
                        }
                        else
                        {
                            i++;
                        }
                    }
                    values.Add(tuple.Substring(start, i - start));
                }
                else if (c == '(')
                {
                    // Function call or expression: match balanced parens
                    int start = i;
                    int depth = 1;
                    i++;
                    while (i < tuple.Length && depth > 0)
                    {
                        if (tuple[i] == '(') depth++;
                        else if (tuple[i] == ')') depth--;
                        i++;
                    }
                    values.Add(tuple.Substring(start, i - start));
                }
                else if (c == ',')
                {
                    // Empty value between commas
                    values.Add("");
                    i++;
                }
                else
                {
                    // Number, NULL, keyword: read until comma or end
                    int start = i;
                    while (i < tuple.Length && tuple[i] != ',')
                    {
                        // If we hit a paren inside a value expression, skip to matching close
                        if (tuple[i] == '(')
                        {
                            int depth = 1;
                            i++;
                            while (i < tuple.Length && depth > 0)
                            {
                                if (tuple[i] == '(') depth++;
                                else if (tuple[i] == ')') depth--;
                                i++;
                            }
                        }
                        else
                        {
                            i++;
                        }
                    }
                    values.Add(tuple.Substring(start, i - start).Trim());
                }

                // Skip comma
                if (i < tuple.Length && tuple[i] == ',') i++;
            }

            return values;
        }
    }
}
