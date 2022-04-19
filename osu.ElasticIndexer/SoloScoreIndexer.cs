// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Elasticsearch.Net;
using MySqlConnector;
using Nest;
using StatsdClient;

namespace osu.ElasticIndexer
{
    public class SoloScoreIndexer : IIndexer
    {
        struct Metadata
        {
            public ulong LastId { get; set; }
            public string RealName { get; set; }
            public ulong? ResetQueueTo { get; set; }
            public string Schema { get; set; }
            public DateTimeOffset? UpdatedAt { get; set; }
            public bool Ready { get; set; }
        }

        public event EventHandler<IndexCompletedArgs> IndexCompleted = delegate { };

        public string Name { get; set; }
        public ulong? ResumeFrom { get; set; }
        public string Suffix { get; set; }

        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        private BulkIndexingDispatcher<SoloScore> dispatcher;

        // metadata
        private Metadata metadata = new Metadata();

        public void Run()
        {
            if (!checkIfReady()) return;

            getIndexMeta();

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
                        saveMetadata();
                    }
                    else
                        updateAlias(Name, metadata.RealName);

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

            void handleBatchWithLastIdCompleted(object sender, ulong lastId)
            {
                metadata.LastId = lastId;
                // metadata.UpdatedAt = DateTimeOffset.UtcNow;
                saveMetadata();
            }
        }

        /// <summary>
        /// Checks if the indexer should wait for the next pass or continue.
        /// </summary>
        /// <returns>true if ready; false, otherwise.</returns>
        private bool checkIfReady()
        {
            if (AppSettings.IsRebuild || IndexMeta.GetByAliasForCurrentVersion(Name).FirstOrDefault() != null)
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
        private Task<long> databaseReaderTask(ulong resumeFrom) => Task.Factory.StartNew(() =>
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

        /// <summary>
        /// Attempts to find the matching index or creates a new one.
        /// </summary>
        /// <param name="name">name of the index alias.</param>
        /// <returns>Name of index found or created and any existing alias.</returns>
        private (string index, bool aliased) findOrCreateIndex(string name)
        {
            Console.WriteLine();

            var aliasedIndices = elasticClient.GetIndicesPointingToAlias(name);
            // Get indices matching pattern with correct schema version.
            var indices = elasticClient.Indices.Get($"{name}_*").Indices;
            var indexNames = new List<string>();
            foreach (var (indexName, indexState) in indices)
                if ((string) indexState.Mappings.Meta["schema"] == AppSettings.Schema)
                    indexNames.Add(indexName.Name);

            string index = null;

            if (!AppSettings.IsNew)
            {
                index = indexNames.FirstOrDefault(name => aliasedIndices.Contains(name));
                // 3 cases are handled:
                // 1. Index was already aliased and has tracking information; likely resuming from a completed job.
                if (index != null)
                {
                    Console.WriteLine($"Using alias `{index}`.");
                    metadata.RealName = index;
                    return (index, aliased: true);
                }

                // 2. Index has not been aliased and has tracking information;
                // likely resuming from an incomplete job or waiting to switch over.
                // TODO: throw if there's more than one? or take lastest one.
                index = indexNames.FirstOrDefault();
                if (index != null)
                {
                    Console.WriteLine($"Using non-aliased `{index}`.");
                    metadata.RealName = index;
                    return (index, aliased: false);
                }
            }

            if (!AppSettings.IsRebuild && index == null)
                throw new Exception("no existing index found");

            // 3. Not aliased and no tracking information; likely starting from scratch
            var suffix = Suffix ?? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            index = $"{name}_{suffix}";
            metadata.RealName = index;

            Console.WriteLine($"Creating `{index}` for `{name}`.");

            // create by supplying the json file instead of the attributed class because we're not
            // mapping every field but still want everything for _source.
            var json = File.ReadAllText(Path.GetFullPath("schemas/solo_scores.json"));
            elasticClient.LowLevel.Indices.Create<DynamicResponse>(index, json);
            metadata.Schema = AppSettings.Schema;
            saveMetadata();

            return (index, aliased: false);

            // TODO: cases not covered should throw an Exception (aliased but not tracked, etc).
        }

        private void getIndexMeta()
        {
            // TODO: all this needs cleaning.
            var (index, aliased) = findOrCreateIndex(Name);

            if (!AppSettings.IsRebuild && !aliased)
                updateAlias(Name, index);

            metadata.LastId = ResumeFrom ?? metadata.LastId;
            metadata.RealName = index;

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

        private List<KeyValuePair<IndexName, IndexState>> getIndicesForCurrentVersion(string name)
        {
            return elasticClient.Indices.Get($"{name}_*").Indices
                .Where(entry => (string) entry.Value.Mappings.Meta["schema"] == AppSettings.Schema)
                .ToList();
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

        private void updateAlias(string alias, string index, bool close = true)
        {
            // TODO: updating alias should mark the index as ready since it's switching over.
            Console.WriteLine($"Updating `{alias}` alias to `{index}`...");

            var aliasDescriptor = new BulkAliasDescriptor();
            var oldIndices = elasticClient.GetIndicesPointingToAlias(alias);

            foreach (var oldIndex in oldIndices)
                aliasDescriptor.Remove(d => d.Alias(alias).Index(oldIndex));

            aliasDescriptor.Add(d => d.Alias(alias).Index(index));
            elasticClient.Indices.BulkAlias(aliasDescriptor);

            // cleanup
            if (!close) return;
            foreach (var toClose in oldIndices.Where(x => x != index))
            {
                Console.WriteLine($"Closing {toClose}");
                elasticClient.Indices.Close(toClose);
            }
        }

        private void saveMetadata()
        {
            elasticClient.Map<SoloScoreIndexer>(mappings => mappings.Meta(
                m => m
                    .Add("last_id", metadata.LastId)
                    .Add("reset_queue_to", metadata.ResetQueueTo)
                    .Add("schema", metadata.Schema)
                    .Add("ready", metadata.Ready)
                    .Add("updated_at", DateTimeOffset.UtcNow)
            ).Index(metadata.RealName));
        }
    }
}
