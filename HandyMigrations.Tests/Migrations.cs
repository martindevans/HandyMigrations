using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using HandyMigrations.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandyMigrations.Tests
{
    [TestClass]
    public class Migrations
    {
        private static IServiceProvider Setup<TMigrationManager>()
            where TMigrationManager : class, IMigrationManager
        {
            var services = new ServiceCollection();

            // Open DB connection
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            services.AddSingleton(conn);
            services.AddSingleton<DbConnection>(conn);
            services.AddSingleton<IDbConnection>(conn);

            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddTransient<IMigrationManager, TMigrationManager>();

            var provider = services.BuildServiceProvider();

            return provider;
        }


        [TestMethod]
        public async Task ApplyNoMigrations()
        {
            var provider = Setup<EmptyMigrationManager>();

            var version = await provider.GetRequiredService<IMigrationManager>().Apply();

            Assert.AreEqual(0, version);
        }

        [TestMethod]
        public async Task ApplyNoMigrationsTwice()
        {
            var provider = Setup<EmptyMigrationManager>();

            var version1 = await provider.GetRequiredService<IMigrationManager>().Apply();
            var version2 = await provider.GetRequiredService<IMigrationManager>().Apply();

            Assert.AreEqual(0, version1);
            Assert.AreEqual(0, version2);
        }

        [TestMethod]
        public async Task AddTable()
        {
            var provider = Setup<AddTableMigrationManager>();

            var version = await provider.GetRequiredService<IMigrationManager>().Apply();

            Assert.AreEqual(1, version);
        }

        [TestMethod]
        public async Task AddTableTwice()
        {
            var provider = Setup<AddTableMigrationManager>();

            var version1 = await provider.GetRequiredService<IMigrationManager>().Apply();
            var version2 = await provider.GetRequiredService<IMigrationManager>().Apply();

            Assert.AreEqual(1, version1);
            Assert.AreEqual(1, version2);
        }

        [TestMethod]
        [ExpectedException(typeof(MigrationVersionTooHighException))]
        public async Task ApplyMigrationsToNewerVersion()
        {
            var provider = Setup<AddTableMigrationManager>();

            var version = await provider.GetRequiredService<IMigrationManager>().Apply();
            Assert.AreEqual(1, version);

            await ActivatorUtilities.GetServiceOrCreateInstance<EmptyMigrationManager>(provider).Apply();
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class EmptyMigrationManager
            : MigrationManager
        {
            public EmptyMigrationManager(ILogger logger, DbConnection db, IServiceProvider services)
                : base(logger, db, services, Migrations())
            {
            }

            private static IReadOnlyList<Type> Migrations()
            {
                return Array.Empty<Type>();
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class AddTableMigrationManager
            : MigrationManager
        {
            public AddTableMigrationManager(ILogger logger, DbConnection db, IServiceProvider services)
                : base(logger, db, services, Migrations())
            {
            }

            private static IReadOnlyList<Type> Migrations()
            {
                return new[] {
                    typeof(AddTableMigration)
                };
            }
        }

        private class AddTableMigration
            : IMigration
        {
            public Task Apply(DbTransaction tsx)
            {
                return tsx.CreateTable(new("test_table") {
                    new("col1", ColumnType.Integer)
                });
            }
        }
    }
}