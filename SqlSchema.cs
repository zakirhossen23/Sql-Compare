using System.Collections.Generic;

namespace Sql_Compare
{
    public class DatabaseSchema
    {
        public List<TableSchema> Tables { get; set; } = new List<TableSchema>();
    }

    public class TableSchema
    {
        public string Name { get; set; }
        public string Schema { get; set; } = "";
        public List<ColumnSchema> Columns { get; set; } = new List<ColumnSchema>();
        public PrimaryKeySchema PrimaryKey { get; set; }
        public List<ForeignKeySchema> ForeignKeys { get; set; } = new List<ForeignKeySchema>();
        public List<IndexSchema> Indexes { get; set; } = new List<IndexSchema>();
        public List<UniqueConstraintSchema> UniqueConstraints { get; set; } = new List<UniqueConstraintSchema>();
        public List<CheckConstraintSchema> CheckConstraints { get; set; } = new List<CheckConstraintSchema>();
        /// <summary>
        /// Row data extracted from INSERT statements. Each row is colName -> value.
        /// </summary>
        public List<Dictionary<string, string>> Rows { get; set; } = new List<Dictionary<string, string>>();

        public string FullName => string.IsNullOrEmpty(Schema) ? $"[{Name}]" : $"[{Schema}].[{Name}]";
    }

    public class ColumnSchema
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsUnsigned { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public string DefaultValue { get; set; }
        public bool IsComputed { get; set; }
        public string ComputedDefinition { get; set; }
        public string Collation { get; set; }
        public bool IsRowGuidCol { get; set; }
        public string GeneratedAlwaysType { get; set; } // AS ROW START, AS ROW END, etc.

        public string GetDataTypeDisplay()
        {
            if (IsComputed) return $"AS {ComputedDefinition}";
            
            var type = DataType.ToUpper();
            if (type == "NVARCHAR" || type == "VARCHAR" || type == "NCHAR" || type == "CHAR" || type == "BINARY" || type == "VARBINARY")
            {
                if (MaxLength == -1) return $"{DataType}(MAX)";
                if (MaxLength.HasValue) return $"{DataType}({MaxLength})";
                return DataType;
            }
            if (type == "DECIMAL" || type == "NUMERIC" || type == "FLOAT" || type == "REAL")
            {
                if (Precision.HasValue && Scale.HasValue) return $"{DataType}({Precision},{Scale})";
                if (Precision.HasValue) return $"{DataType}({Precision})";
                return DataType;
            }
            if (type == "DATETIME2" || type == "DATETIMEOFFSET" || type == "TIME")
            {
                if (Precision.HasValue) return $"{DataType}({Precision})";
                return DataType;
            }
            // For integer types, don't add display width
            return DataType + (IsUnsigned ? " unsigned" : "");
        }
    }

    public class PrimaryKeySchema
    {
        public string Name { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public bool IsClustered { get; set; } = true;
    }

    public class ForeignKeySchema
    {
        public string Name { get; set; }
        public string SourceTable { get; set; }
        public List<string> SourceColumns { get; set; } = new List<string>();
        public string ReferencedTable { get; set; }
        public string ReferencedTableSchema { get; set; } = "dbo";
        public List<string> ReferencedColumns { get; set; } = new List<string>();
        public string OnDelete { get; set; }
        public string OnUpdate { get; set; }

        public string GetReferencedTableFullName() => $"`{ReferencedTableSchema}`.`{ReferencedTable}`";
    }

    public class IndexSchema
    {
        public string Name { get; set; }
        public string TableName { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public List<string> IncludedColumns { get; set; } = new List<string>();
        public bool IsUnique { get; set; }
        public bool IsClustered { get; set; }
        public string Filter { get; set; }
    }

    public class UniqueConstraintSchema
    {
        public string Name { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
    }

    public class CheckConstraintSchema
    {
        public string Name { get; set; }
        public string Definition { get; set; }
    }

    public enum DiffType
    {
        TableAdded,
        TableRemoved,
        ColumnAdded,
        ColumnRemoved,
        ColumnModified,
        PrimaryKeyAdded,
        PrimaryKeyRemoved,
        PrimaryKeyModified,
        ForeignKeyAdded,
        ForeignKeyRemoved,
        IndexAdded,
        IndexRemoved,
        IndexModified,
        UniqueConstraintAdded,
        UniqueConstraintRemoved,
        CheckConstraintAdded,
        CheckConstraintRemoved,
        RowAdded,
        RowRemoved,
        RowModified
    }

    public class DiffItem
    {
        public DiffType Type { get; set; }
        public string TableName { get; set; }
        public string ObjectName { get; set; }
        public string Description { get; set; }
        public string DetailOld { get; set; }
        public string DetailNew { get; set; }
        public string SqlScript { get; set; }
    }

    public class CompareResult
    {
        public List<DiffItem> Items { get; set; } = new List<DiffItem>();
        public string LocalFileName { get; set; }
        public string ServerFileName { get; set; }

        public string GenerateExportScript()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("-- =======================================================");
            sb.AppendLine("-- SQL Compare Export Script (MariaDB/MySQL)");
            sb.AppendLine($"-- Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"-- Source (Local): {System.IO.Path.GetFileName(LocalFileName)}");
            sb.AppendLine($"-- Target (Server): {System.IO.Path.GetFileName(ServerFileName)}");
            sb.AppendLine("-- =======================================================");
            sb.AppendLine("/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;");
            sb.AppendLine("/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;");
            sb.AppendLine("/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;");
            sb.AppendLine("/*!40101 SET NAMES utf8mb4 */;");
            sb.AppendLine("/*!40101 SET FOREIGN_KEY_CHECKS=0 */;");
            sb.AppendLine();

            foreach (var item in Items)
            {
                if (!string.IsNullOrEmpty(item.SqlScript))
                {
                    sb.AppendLine(item.SqlScript);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("/*!40101 SET FOREIGN_KEY_CHECKS=1 */;");
            sb.AppendLine("-- =======================================================");
            sb.AppendLine("-- End of export script");
            sb.AppendLine("-- =======================================================");

            return sb.ToString();
        }
    }
}
