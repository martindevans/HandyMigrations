using System;

namespace HandyMigrations.Exceptions
{
    /// <summary>
    /// Thrown when encountering a database with an incorrect appid
    /// </summary>
    public class AppIdMismatchException
        : Exception
    {
        public string Expected { get; }
        public string Actual { get; }

        public AppIdMismatchException(string expected, string actual)
        {
            Expected = expected;
            Actual = actual;
        }
    }
}
