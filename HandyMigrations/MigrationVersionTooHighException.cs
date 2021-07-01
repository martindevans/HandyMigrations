using System;

namespace HandyMigrations
{
    public class MigrationVersionTooHighException
        : Exception
    {
        public int Actual { get; }
        public int Maximum { get; }

        public MigrationVersionTooHighException(int actual, int maximum)
        {
            Actual = actual;
            Maximum = maximum;
        }

        public override string ToString()
        {
            return $"Database version is `{Actual}`, but max version supported by this application is `{Maximum}`";
        }
    }
}
