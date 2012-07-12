#region License
// 
// Copyright (c) 2007-2009, Sean Chambers <schambers80@gmail.com>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentMigrator.Expressions;
using FluentMigrator.Infrastructure;
using FluentMigrator.Runner.Initialization;

namespace FluentMigrator.Runner
{
    public class MigrationRunner : IMigrationRunner
    {
        private readonly Assembly migrationAssembly;
        private readonly IAnnouncer announcer;
        private readonly IStopWatch stopWatch;
        private bool alreadyOutputPreviewOnlyModeWarning;

        /// <summary>The arbitrary application context passed to the task runner.</summary>
        public object ApplicationContext { get; private set; }

        public bool SilentlyFail { get; set; }

        public IMigrationProcessor Processor { get; private set; }
        public IMigrationLoader MigrationLoader { get; set; }
        public IProfileLoader ProfileLoader { get; set; }
        public IMigrationConventions Conventions { get; private set; }
        public IList<Exception> CaughtExceptions { get; private set; }

        public MigrationRunner(Assembly assembly, IRunnerContext runnerContext, IMigrationProcessor processor)
        {
            migrationAssembly = assembly;
            announcer = runnerContext.Announcer;
            Processor = processor;
            stopWatch = runnerContext.StopWatch;
            ApplicationContext = runnerContext.ApplicationContext;

            SilentlyFail = false;
            CaughtExceptions = null;

            Conventions = new MigrationConventions();
            if (!string.IsNullOrEmpty(runnerContext.WorkingDirectory))
                Conventions.GetWorkingDirectory = () => runnerContext.WorkingDirectory;

            VersionLoader = new VersionLoader(this, migrationAssembly, Conventions);
            MigrationLoader = new MigrationLoader(Conventions, migrationAssembly, runnerContext.Namespace, runnerContext.Tags);
            ProfileLoader = new ProfileLoader(runnerContext, this, Conventions);
        }

        public IVersionLoader VersionLoader { get; set; }

        public void ApplyProfiles()
        {
            ProfileLoader.ApplyProfiles();
        }

        public void MigrateUp()
        {
            MigrateUp(true);
        }

        public void MigrateUp(bool useAutomaticTransactionManagement)
        {
            try
            {
                foreach (var version in MigrationLoader.Migrations.Keys)
                {
                    ApplyMigrationUp(version);
                }

                ApplyProfiles();

                if (useAutomaticTransactionManagement) { Processor.CommitTransaction(); }
                VersionLoader.LoadVersionInfo();
            }
            catch (Exception)
            {
                if (useAutomaticTransactionManagement) { Processor.RollbackTransaction(); }
                throw;
            }
        }

        public void MigrateUp(long targetVersion)
        {
            MigrateUp(targetVersion, true);
        }

        public void MigrateUp(long targetVersion, bool useAutomaticTransactionManagement)
        {
            try
            {
                foreach (var neededMigrationVersion in GetUpMigrationsToApply(targetVersion))
                {
                    ApplyMigrationUp(neededMigrationVersion);
                }
                if (useAutomaticTransactionManagement) { Processor.CommitTransaction(); }
                VersionLoader.LoadVersionInfo();
            }
            catch (Exception)
            {
                if (useAutomaticTransactionManagement) { Processor.RollbackTransaction(); }
                throw;
            }
        }

        private IEnumerable<long> GetUpMigrationsToApply(long version)
        {
            return MigrationLoader.Migrations.Keys.Where(x => IsMigrationStepNeededForUpMigration(x, version));
        }

        private bool IsMigrationStepNeededForUpMigration(long versionOfMigration, long targetVersion)
        {
            return versionOfMigration <= targetVersion && !VersionLoader.VersionInfo.HasAppliedMigration(versionOfMigration);
        }

        public void MigrateDown(long targetVersion)
        {
            MigrateDown(targetVersion, true);
        }

        public void MigrateDown(long targetVersion, bool useAutomaticTransactionManagement)
        {
            try
            {
                foreach (var neededMigrationVersion in GetDownMigrationsToApply(targetVersion))
                {
                    ApplyMigrationDown(neededMigrationVersion);
                }

                if (useAutomaticTransactionManagement) { Processor.CommitTransaction(); }
                VersionLoader.LoadVersionInfo();
            }
            catch (Exception)
            {
                if (useAutomaticTransactionManagement) { Processor.RollbackTransaction(); }
                throw;
            }
        }

        private IEnumerable<long> GetDownMigrationsToApply(long targetVersion)
        {
            return MigrationLoader.Migrations.Keys.Where(x => IsMigrationStepNeededForDownMigration(x, targetVersion)).Reverse();
        }

        private bool IsMigrationStepNeededForDownMigration(long versionOfMigration, long targetVersion)
        {
            return versionOfMigration > targetVersion && VersionLoader.VersionInfo.HasAppliedMigration(versionOfMigration);
        }

        private void ApplyMigrationUp(long version)
        {
            if (!alreadyOutputPreviewOnlyModeWarning && Processor.Options.PreviewOnly)
            {
                announcer.Heading("PREVIEW-ONLY MODE");
                alreadyOutputPreviewOnlyModeWarning = true;
            }

            if (!VersionLoader.VersionInfo.HasAppliedMigration(version))
            {
                Up(MigrationLoader.Migrations[version]);
                VersionLoader.UpdateVersionInfo(version);
            }
        }

        private void ApplyMigrationDown(long version)
        {
            try
            {
                Down(MigrationLoader.Migrations[version]);
                VersionLoader.DeleteVersion(version);
            }
            catch (KeyNotFoundException ex)
            {
                string msg = string.Format("VersionInfo references version {0} but no Migrator was found attributed with that version.", version);
                throw new Exception(msg, ex);
            }
            catch (Exception ex)
            {
                throw new Exception("Error rolling back version " + version, ex);
            }
        }

        public void Rollback(int steps)
        {
            Rollback(steps, true);
        }

        public void Rollback(int steps, bool useAutomaticTransactionManagement)
        {
            try
            {
                var migrations = VersionLoader.VersionInfo.AppliedMigrations().Intersect(MigrationLoader.Migrations.Keys);

                foreach (var migrationNumber in migrations.Take(steps))
                {
                    ApplyMigrationDown(migrationNumber);
                }

                VersionLoader.LoadVersionInfo();

                if (!VersionLoader.VersionInfo.AppliedMigrations().Any())
                    VersionLoader.RemoveVersionTable();

                if (useAutomaticTransactionManagement) { Processor.CommitTransaction(); }
            }
            catch (Exception)
            {
                if (useAutomaticTransactionManagement) { Processor.RollbackTransaction(); }
                throw;
            }
        }

        public void RollbackToVersion(long version)
        {
            RollbackToVersion(version, true);
        }

        public void RollbackToVersion(long version, bool useAutomaticTransactionManagement)
        {
            try
            {
                var migrations = VersionLoader.VersionInfo.AppliedMigrations().Intersect(MigrationLoader.Migrations.Keys);

                // Get the migrations between current and the to version
                foreach (var migrationNumber in migrations)
                {
                    if (version < migrationNumber)
                    {
                        ApplyMigrationDown(migrationNumber);
                    }
                }

                VersionLoader.LoadVersionInfo();

                if (version == 0 && !VersionLoader.VersionInfo.AppliedMigrations().Any())
                    VersionLoader.RemoveVersionTable();

                if (useAutomaticTransactionManagement) { Processor.CommitTransaction(); }
            }
            catch (Exception)
            {
                if (useAutomaticTransactionManagement) { Processor.RollbackTransaction(); }
                throw;
            }
        }

        public Assembly MigrationAssembly
        {
            get { return migrationAssembly; }
        }

        private string GetMigrationName(IMigration migration)
        {
            if (migration == null) throw new ArgumentNullException("migration");

            var metadata = migration as IMigrationMetadata;
            if (metadata != null)
            {
                return string.Format("{0}: {1}", metadata.Version, metadata.Type.Name);
            }
            return migration.GetType().Name;
        }

        public void Up(IMigration migration)
        {
            var name = GetMigrationName(migration);
            announcer.Heading(string.Format("{0} migrating", name));

            CaughtExceptions = new List<Exception>();

            var context = new MigrationContext(Conventions, Processor, MigrationAssembly, ApplicationContext);
            migration.GetUpExpressions(context);

            stopWatch.Start();
            ExecuteExpressions(context.Expressions);
            stopWatch.Stop();

            announcer.Say(string.Format("{0} migrated", name));
            announcer.ElapsedTime(stopWatch.ElapsedTime());
        }

        public void Down(IMigration migration)
        {
            var name = GetMigrationName(migration);
            announcer.Heading(string.Format("{0} reverting", name));

            CaughtExceptions = new List<Exception>();

            var context = new MigrationContext(Conventions, Processor, MigrationAssembly, ApplicationContext);
            migration.GetDownExpressions(context);

            stopWatch.Start();
            ExecuteExpressions(context.Expressions);
            stopWatch.Stop();

            announcer.Say(string.Format("{0} reverted", name));
            announcer.ElapsedTime(stopWatch.ElapsedTime());
        }

        /// <summary>
        /// execute each migration expression in the expression collection
        /// </summary>
        /// <param name="expressions"></param>
        private void ExecuteExpressions(IEnumerable<IMigrationExpression> expressions)
        {
            long insertTicks = 0;
            var insertCount = 0;
            foreach (var expression in expressions)
            {
                try
                {
                    var migrationExpression = expression;
                    migrationExpression.ApplyConventions(Conventions);
                    if (migrationExpression is InsertDataExpression)
                    {
                        insertTicks += Time(() => migrationExpression.ExecuteWith(Processor));
                        insertCount++;
                    }
                    else
                    {
                        AnnounceTime(migrationExpression.ToString(), () => migrationExpression.ExecuteWith(Processor));
                    }
                }
                catch (Exception er)
                {
                    announcer.Error(er.Message);

                    //catch the error and move onto the next expression
                    if (SilentlyFail)
                    {
                        CaughtExceptions.Add(er);
                        continue;
                    }
                    throw;
                }
            }

            if (insertCount > 0)
            {
                var avg = new TimeSpan(insertTicks / insertCount);
                var msg = string.Format("-> {0} Insert operations completed in {1} taking an average of {2}", insertCount, new TimeSpan(insertTicks), avg);
                announcer.Say(msg);
            }
        }

        private void AnnounceTime(string message, Action action)
        {
            announcer.Say(message);

            stopWatch.Start();
            action();
            stopWatch.Stop();

            announcer.ElapsedTime(stopWatch.ElapsedTime());
        }

        private long Time(Action action)
        {
            stopWatch.Start();

            action();

            stopWatch.Stop();

            return stopWatch.ElapsedTime().Ticks;
        }
    }
}