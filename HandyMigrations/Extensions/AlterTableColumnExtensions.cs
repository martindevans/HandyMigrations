using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace HandyMigrations.Extensions
{
    public static class AlterTableColumnExtensions
    {
        public static async Task AlterTableAddColumn(this IDbTransaction tsx, string table, TableColumn column)
        {
            if (column.Attr.HasFlag(ColumnAttributes.PrimaryKey))
                throw new NotImplementedException("Alter table add primary key");

            var sql = $"ALTER TABLE '{table}' ADD COLUMN {column.ToSql(ColumnAttributes.Unique, true)}";
            await tsx.Connection.ExecuteAsync(sql, transaction: tsx);

            if (column.Attr.HasFlag(ColumnAttributes.Unique))
            {
                await tsx.Connection.ExecuteAsync(
                    $"CREATE UNIQUE INDEX '{table}_{column.Name}_IsUnique' ON '{table}'('{column.Name}');",
                    transaction: tsx
                );
            }
        }
    }
}
