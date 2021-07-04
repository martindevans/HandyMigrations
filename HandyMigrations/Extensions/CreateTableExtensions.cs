using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace HandyMigrations.Extensions
{
    public static class CreateTableExtensions
    {
        public static async Task CreateTable(this IDbTransaction tsx, Table table)
        {
            await tsx.Connection.ExecuteAsync(
                table.ToSql(),
                transaction: tsx
            );
        }
    }

    public class Table
        : IEnumerable<TableColumn>
    {
        private readonly string _name;
        private readonly PrimaryKey? _primaryKey;
        private readonly List<TableColumn> _columns = new();

        public Table(string name, PrimaryKey? primaryKey = null)
        {
            _name = name;
            _primaryKey = primaryKey;
        }

        public IEnumerator<TableColumn> GetEnumerator()
        {
            return _columns.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(TableColumn column)
        {
            _columns.Add(column);
        }

        internal string ToSql()
        {
            var builder = new StringBuilder();
            builder.Append($"CREATE TABLE '{_name}' (");

            builder.AppendJoin(",", _columns.Select(c => c.ToSql()));

            if (_primaryKey != null)
            {
                builder.Append(',');
                builder.Append(_primaryKey.ToSql());
            }

            if (_columns.Any(c => c.ForeignKey != null))
            {
                builder.Append(',');
                builder.AppendJoin(",", _columns.Where(c => c.ForeignKey != null).Select(c => c.ForeignKey!.ToSql(c.Name)));
            }

            builder.Append($");");

            return builder.ToString();
        }
    }

    public class TableColumn
    {
        public string Name { get; }
        public ColumnType Type { get; }
        public ColumnAttributes Attr { get; }
        public ForeignKey? ForeignKey { get; }

        public TableColumn(string name, ColumnType type, ColumnAttributes attr = ColumnAttributes.None, ForeignKey? foreignKey = null)
        {
            Name = name;
            Type = type;
            Attr = attr;
            ForeignKey = foreignKey;
        }

        internal string ToSql(ColumnAttributes attrRemove = ColumnAttributes.None, bool includeForeign = false)
        {
            var attrsBuilder = new StringBuilder("");
            if (Attr != ColumnAttributes.None)
            {
                foreach (var value in Enum.GetValues<ColumnAttributes>())
                {
                    if (!Attr.HasFlag(value) && !attrRemove.HasFlag(value))
                        continue;

                    var attr = value switch {
                        ColumnAttributes.None => "",
                        ColumnAttributes.NotNull => "NOT NULL",
                        _ => value.ToString().ToUpper()
                    };
                    attrsBuilder.Append(attr);
                    attrsBuilder.Append(' ');
                    
                }
            }

            var str = $"'{Name}' {Type} {attrsBuilder}";

            if (includeForeign && ForeignKey != null)
                str += $" REFERENCES '{ForeignKey.ForeignTable}'('{ForeignKey.ForeignColumn}')";

            return str;
        }
    }

    public class ForeignKey
    {
        public string ForeignTable { get; }
        public string ForeignColumn { get; }

        public ForeignKey(string foreignTable, string foreignColumn)
        {
            ForeignTable = foreignTable;
            ForeignColumn = foreignColumn;
        }

        internal string ToSql(string column) => $"FOREIGN KEY('{column}') REFERENCES '{ForeignTable}'('{ForeignColumn}')";
    }

    public class PrimaryKey
    {
        public IReadOnlyList<string> Columns { get; }

        public PrimaryKey(params string[] columns)
        {
            Columns = columns;
        }

        internal string ToSql() => $"PRIMARY KEY({string.Join(",", Columns.Select(c => $"'{c}'"))})";
    }

    public enum ColumnType
    {
        Integer,
        Real,
        Text,
        Blob
    }

    [Flags]
    public enum ColumnAttributes
    {
        None = 0,
        NotNull = 1,
        PrimaryKey = 2,
        Unique = 4,
        AutoIncrement = 8,
    }
}
