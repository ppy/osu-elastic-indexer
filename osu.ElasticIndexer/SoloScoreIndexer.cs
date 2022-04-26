// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using Nest;

namespace osu.ElasticIndexer
{
    public class SoloScoreIndexer : IIndexer
    {
        public event EventHandler<IndexCompletedArgs> IndexCompleted = delegate { };

        public string Name { get; set; }
        public long? ResumeFrom { get; set; }

        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        private BulkIndexingDispatcher<SoloScore> dispatcher;

        private Metadata metadata;

        public void Run()
        {
            if (!checkIfReady()) return;

            this.metadata = IndexHelper.FindOrCreateIndex(Name);

            checkIndexState();

            var indexCompletedArgs = new IndexCompletedArgs
            {
                Alias = Name,
                Index = metadata.RealName,
                StartedAt = DateTime.Now
            };

            dispatcher = new BulkIndexingDispatcher<SoloScore>(metadata.RealName);

            if (AppSettings.IsRebuild)
                dispatcher.BatchWithLastIdCompleted += handleBatchWithLastIdCompleted;

            try
            {
                var readerTask = databaseReaderTask(metadata.LastId);
                dispatcher.Run();
                readerTask.Wait();

                indexCompletedArgs.Count = readerTask.Result;
                indexCompletedArgs.CompletedAt = DateTime.Now;

                // when preparing for schema changes, the alias update
                // should be done by process waiting for the ready signal.
                if (AppSettings.IsRebuild)
                    if (AppSettings.IsPrepMode) {
                        metadata.Ready = true;
                        metadata.Save();
                    }
                    else
                        IndexHelper.UpdateAlias(Name, metadata.RealName);

                IndexCompleted(this, indexCompletedArgs);
            }
            catch (AggregateException ae)
            {
                ae.Handle(handleAggregateException);
            }

            // Local function exception handler.
            bool handleAggregateException(Exception ex)
            {
                if (!(ex is InvalidOperationException)) return false;

                Console.Error.WriteLine(ex.Message);
                if (ex.InnerException != null)
                    Console.Error.WriteLine(ex.InnerException.Message);

                return true;
            }

            void handleBatchWithLastIdCompleted(object sender, long lastId)
            {
                metadata.LastId = lastId;
                // metadata.UpdatedAt = DateTimeOffset.UtcNow;
                metadata.Save();
            }
        }

        /// <summary>
        /// Checks if the indexer should wait for the next pass or continue.
        /// </summary>
        /// <returns>true if ready; false, otherwise.</returns>
        private bool checkIfReady()
        {
            if (AppSettings.IsRebuild || IndexHelper.GetIndicesForCurrentVersion(Name).Count > 0)
                return true;

            Console.WriteLine($"`{Name}` for version {AppSettings.Schema} is not ready...");
            return false;
        }

        /// <summary>
        /// Self contained database reader task. Reads the database by cursoring through records
        /// and adding chunks into readBuffer.
        /// </summary>
        /// <param name="resumeFrom">The cursor value to resume from;
        /// use null to resume from the last known value.</param>
        /// <returns>The database reader task.</returns>
        private Task<long> databaseReaderTask(long resumeFrom) => Task.Factory.StartNew(() =>
            {
                long count = 0;

                while (true)
                {
                    try
                    {
                        Console.WriteLine($"Rebuild from {resumeFrom}...");
                        var chunks = Model.Chunk<SoloScore>(AppSettings.ChunkSize, resumeFrom);
                        foreach (var chunk in chunks)
                        {
                            // var scores = SoloScore.FetchByScoreIds(chunk.Select(x => x.score_id).AsList());
                            var scores = chunk.Where(x => x.ShouldIndex).AsList();
                            // TODO: investigate fetching country directly in scores query.
                            var users = User.FetchUserMappings(scores);
                            foreach (var score in scores)
                            {
                                score.country_code = users[score.UserId].country_acronym;
                            }

                            dispatcher.Enqueue(scores);
                            count += chunk.Count;
                            // update resumeFrom in this scope to allow resuming from connection errors.
                            resumeFrom = chunk.Last().CursorValue;
                        }

                        break;
                    }
                    catch (DbException ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                        Task.Delay(1000).Wait();
                    }
                }

                dispatcher.EnqueueEnd();
                Console.WriteLine($"Finished reading database {count} records.");

                return count;
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);

        private void checkIndexState()
        {
            // TODO: all this needs cleaning.
            if (!AppSettings.IsRebuild && !metadata.IsAliased)
                IndexHelper.UpdateAlias(Name, metadata.RealName);

            metadata.LastId = ResumeFrom ?? metadata.LastId;

            if (metadata.Schema != AppSettings.Schema)
                // A switchover is probably happening, so signal that this mode should be skipped.
                throw new VersionMismatchException($"`{Name}` found version {metadata.Schema}, expecting {AppSettings.Schema}");

            if (AppSettings.IsRebuild)
            {
                // Save the position the score processing queue should be reset to if rebuilding an index.
                // If there is already an existing value, the processor is probabaly resuming from a previous run,
                // so we likely want to preserve that value.
                if (!metadata.ResetQueueTo.HasValue)
                {
                    // TODO: set ResetQueueTo to first unprocessed item.
                }
            }
            else
            {
                // process queue reset if any.
                if (metadata.ResetQueueTo.HasValue)
                {
                    Console.WriteLine($"Resetting queue_id > {metadata.ResetQueueTo}");
                    // TODO: reset queue to metadata.ResetQueueTo.Value
                    metadata.ResetQueueTo = null;
                }
            }
        }

        private Dictionary<uint, dynamic> getUsers(IEnumerable<uint> userIds)
        {
            // get users
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                dbConnection.Open();
                return dbConnection.Query<dynamic>($"select user_id, country_acronym from phpbb_users where user_id in @userIds", new { userIds })
                    .ToDictionary(u => (uint) u.user_id);
            }
        }
    }
}
