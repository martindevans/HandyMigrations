using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HandyMigrations
{
    public interface IMigrationManager
    {
        /// <summary>
        /// Apply all migrations and return the new version number
        /// </summary>
        /// <returns></returns>
        Task<int> Apply();
    }

    public abstract class MigrationManager
        : IMigrationManager
    {
        private readonly ILogger _logger;
        private readonly DbConnection _db;
        private readonly IServiceProvider _services;
        private readonly IReadOnlyList<Type> _migrations;

        protected MigrationManager(ILogger logger, DbConnection db, IServiceProvider services, IReadOnlyList<Type> migrations)
        {
            _logger = logger;
            _db = db;
            _services = services;
            _migrations = migrations;
        }

        public async Task<int> Apply()
        {
            // Get the current version of the migrations that have already been applied to
            // the DB, if this returns null then the DB is uninitialised and we need
            // to run version 0 which means the "current" version is effectively -1
            var current = await GetCurrentVersion();
            _logger.LogDebug("Current DB version: {0}", current);

            // Sanity check that the DB is not _too_ new
            if (current >= _migrations.Count)
                throw new MigrationVersionTooHighException(current, _migrations.Count);

            // Run through all migrations after the currently applied one
            for (var i = current + 1; i < _migrations.Count; i++)
            {
                // Start a transaction for this single migration
                await using var tsx = await _db.BeginTransactionAsync();

                _logger.LogInformation("Finding migration: {0}", i);
                var migration = (IMigration)ActivatorUtilities.GetServiceOrCreateInstance(_services, _migrations[i]);
                _logger.LogInformation("Applying migration: {1}", i, migration);
                await migration.Apply(tsx);

                _db.Insert(new MigrationVersion {VersionApplied = i}, tsx);

                await tsx.CommitAsync();
            }

            return _migrations.Count;
        }

        private async Task<int> GetCurrentVersion()
        {
            // Create the migration table in case this is an uninitialised database
            await using (var tsx = await _db.BeginTransactionAsync())
            {
                await _db.ExecuteAsync("CREATE TABLE IF NOT EXISTS 'MigrationVersions' ('VersionApplied' INTEGER NOT NULL UNIQUE);");
                await _db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS 'MigrationVersionsIndex' ON 'MigrationVersions' ('VersionApplied' ASC);");
                
                await tsx.CommitAsync();
            }

            // Get the maximum version record, returns null if there isn't one (i.e. this is uninitialised)
            var max = _db.Query<MigrationVersion>("SELECT * FROM MigrationVersions ORDER BY VersionApplied DESC LIMIT 1").FirstOrDefault();

            return max?.VersionApplied ?? -1;
        }

        internal class MigrationVersion
        {
            public int VersionApplied { get; set; }
        }
    }
}
