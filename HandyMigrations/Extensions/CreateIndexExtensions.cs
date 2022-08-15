using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace HandyMigrations.Extensions
{
    public static class CreateIndexExtensions
    {
        public static async Task CreateIndex(this IDbTransaction tsx, Index index)
        {
            await tsx.Connection.ExecuteAsync(
                index.ToSql(),
                transaction: tsx
            );
        }
    }

    public class Index
        : IEnumerable<IndexItem>
    {
        private readonly string _name;
        private readonly string _table;
        private readonly bool _unique;
        private readonly List<IndexItem> _items = new();

        public Index(string name, string table, bool unique = false)
        {
            _name = name;
            _table = table;
            _unique = unique;
        }

        public IEnumerator<IndexItem> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(IndexItem column)
        {
            _items.Add(column);
        }

        internal string ToSql()
        {
            var builder = new StringBuilder();
            builder.Append("CREATE ");

            if (_unique)
                builder.Append("UNIQUE ");

            builder.Append($"INDEX \"{_name}\" ON \"{_table}\" (");
            builder.AppendJoin(",", _items.Select(i => i.ToSql()));
            builder.Append(");");

            return builder.ToString();
        }
    }

    public class IndexItem
    {
        public string Column { get; }
        public IndexOrder Order { get; }

        public IndexItem(string column, IndexOrder order = IndexOrder.None)
        {
            Column = column;
            Order = order;
        }

        public string ToSql()
        {
            var order = Order == IndexOrder.None ? "" : Order.ToString();
            return $"\"{Column}\" {order}";
        }
    }

    public enum IndexOrder
    {
        None,
        ASC,
        DESC,
    }
}

/*
 * CREATE UNIQUE INDEX "MessagesByTimestamp" ON "MqttMessages" (
	"Timestamp"	ASC,
	"Id"	ASC
);
 */
