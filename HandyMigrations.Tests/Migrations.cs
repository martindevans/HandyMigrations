using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using HandyMigrations.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
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

        [TestMethod]
        public async Task AddColumnWithForeignKey()
        {
            // Open DB connection
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            await using (var tsx = conn.BeginTransaction())
            {
                await tsx.CreateTable(new("table") {
                    new("column1", ColumnType.Integer)
                });
                await tsx.CreateTable(new("table2") {
                    new("column2", ColumnType.Integer)
                });

                await tsx.CommitAsync();
            }

            await using (var tsx = conn.BeginTransaction())
            {
                await tsx.AlterTableAddColumn("table", new("column2", ColumnType.Integer, ColumnAttributes.None, new ForeignKey("table2", "column1")));

                await tsx.CommitAsync();
            }
        }

        private class EmptyMigrationManager
            : MigrationManager
        {
            public EmptyMigrationManager(DbConnection db, IServiceProvider services)
                : base(db, services, Migrations())
            {
            }

            private static IReadOnlyList<Type> Migrations()
            {
                return Array.Empty<Type>();
            }
        }

        private class AddTableMigrationManager
            : MigrationManager
        {
            public AddTableMigrationManager(DbConnection db, IServiceProvider services)
                : base(db, services, Migrations())
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
