using System.Data.Common;
using System.Threading.Tasks;

namespace HandyMigrations
{
    public interface IMigration
    {
        Task Apply(DbTransaction tsx);
    }
}
