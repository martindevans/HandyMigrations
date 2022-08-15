using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using HandyMigrations.Exceptions;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly string _appid;

        private readonly DbConnection _db;
        private readonly IServiceProvider _services;
        private readonly IReadOnlyList<Type> _migrations;

        /// <summary>
        /// Create a new migration manager
        /// </summary>
        /// <param name="appid">Unique ID for this application.</param>
        /// <param name="db"></param>
        /// <param name="services"></param>
        /// <param name="migrations"></param>
        protected MigrationManager(string appid, DbConnection db, IServiceProvider services, IReadOnlyList<Type> migrations)
        {
            _appid = appid;
            _db = db;
            _services = services;
            _migrations = migrations;
        }

        public async Task<int> Apply()
        {
            // Check that this is a database for the current app
            await CheckAppId();

            // Get the current version of the migrations that have already been applied to
            // the DB, if this returns -1 then the DB is uninitialised and we need
            // to run version 0
            var current = await GetCurrentVersion();

            // Sanity check that the DB is not _too_ new
            if (current >= _migrations.Count)
                throw new MigrationVersionTooHighException(current, _migrations.Count);

            // Run through all migrations after the currently applied one
            for (var i = current + 1; i < _migrations.Count; i++)
            {
                // Start a transaction for this single migration
                await using var tsx = await _db.BeginTransactionAsync();

                var migration = (IMigration)ActivatorUtilities.GetServiceOrCreateInstance(_services, _migrations[i]);
                await migration.Apply(tsx);

                _db.Insert(new MigrationVersion {VersionApplied = i}, tsx);

                await tsx.CommitAsync();
            }

            return _migrations.Count;
        }

        private async Task CheckAppId()
        {
            // Create the id table in case this is an uninitialised database
            await using (var tsx = await _db.BeginTransactionAsync())
            {
                await _db.ExecuteAsync("CREATE TABLE IF NOT EXISTS 'AppIds' ('ApplicationId' TEXT NOT NULL);", transaction: tsx);
                await tsx.CommitAsync();
            }

            // Check if the ID is a mismatch
            var id = await _db.QueryFirstOrDefaultAsync<AppId>("SELECT * FROM AppIds");
            if (id != null && id.ApplicationId != _appid)
                throw new AppIdMismatchException(_appid.ToString(), id.ApplicationId.ToString());

            // Insert the ID if it was missing
            await using (var tsx = await _db.BeginTransactionAsync())
                _db.Insert(new AppId(_appid), transaction: tsx);
        }

        private async Task<int> GetCurrentVersion()
        {
            // Create the migration table in case this is an uninitialised database
            await using (var tsx = await _db.BeginTransactionAsync())
            {
                await _db.ExecuteAsync("CREATE TABLE IF NOT EXISTS 'MigrationVersions' ('VersionApplied' INTEGER NOT NULL UNIQUE);", transaction: tsx);
                await _db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS 'MigrationVersionsIndex' ON 'MigrationVersions' ('VersionApplied' ASC);", transaction: tsx);
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

        internal class AppId
        {
            public string ApplicationId { get; set; }

            public AppId(string applicationId)
            {
                ApplicationId = applicationId;
            }
        }
    }
}
