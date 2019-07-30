// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest;

namespace osu.ElasticIndexer
{
    public class HighScoreIndexer<T> : IIndexer where T : HighScore, new()
    {
        public event EventHandler<IndexCompletedArgs> IndexCompleted = delegate { };

        public string Name { get; set; }
        public ulong? ResumeFrom { get; set; }
        public string Suffix { get; set; }

        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        private BulkIndexingDispatcher<T> dispatcher;

        public void Run()
        {
            if (!checkIfReady()) return;

            var initial = initialize();
            var index = initial.Index;

            var indexCompletedArgs = new IndexCompletedArgs
            {
                Alias = Name,
                Index = index,
                StartedAt = DateTime.Now
            };

            dispatcher = new BulkIndexingDispatcher<T>(index);
            if (AppSettings.IsRebuild)
                dispatcher.BatchWithLastIdCompleted += handleBatchWithLastIdCompleted;

            try
            {
                var readerTask = databaseReaderTask(initial.LastId);
                dispatcher.Run();
                readerTask.Wait();

                indexCompletedArgs.Count = readerTask.Result;
                indexCompletedArgs.CompletedAt = DateTime.Now;

                IndexMeta.Refresh();

                // when preparing for schema changes, the alias update
                // should be done by process waiting for the ready signal.
                if (AppSettings.IsPrep.Contains(typeof(T)))
                    IndexMeta.MarkAsReady(index);
                else
                    updateAlias(Name, index);

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

                Console.WriteLine(ex.Message);
                return true;
            }

            void handleBatchWithLastIdCompleted(object sender, ulong lastId)
            {
                // TODO: should probably aggregate responses and update to highest successful.
                IndexMeta.UpdateAsync(new IndexMeta
                {
                    Index = index,
                    Alias = Name,
                    LastId = lastId,
                    ResetQueueTo = initial.ResetQueueTo,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        private bool checkIfReady()
        {
            if (!AppSettings.IsPrep.Contains(typeof(T)) || AppSettings.IsRebuild)
                return true;

            Console.WriteLine();
            var indexMeta = IndexMeta.GetPrepIndex(Name);

            if (indexMeta == null)
            {

                Console.WriteLine($"{Name} is not ready...");
                return false;
            }
            else
            {
                Console.WriteLine($"{Name} is ready");
                // update alias and go
                var index = findOrCreateIndex(Name);
                updateAlias(Name, index);
                AppSettings.IsPrep.Remove(typeof(T));

                return true;
            }
        }

        /// <summary>
        /// Self contained database reader task. Reads the database by cursoring through records
        /// and adding chunks into readBuffer.
        /// </summary>
        /// <param name="resumeFrom">The cursor value to resume from;
        /// use null to resume from the last known value.</param>
        /// <returns>The database reader task.</returns>
        private Task<long> databaseReaderTask(ulong resumeFrom)
        {
            return Task.Factory.StartNew(() =>
                {
                    long count = 0;

                    while (true)
                    {
                        try
                        {
                            if (!AppSettings.IsRebuild)
                            {
                                Console.WriteLine("Reading from queue...");
                                var mode = HighScore.GetRulesetId<T>();
                                var chunks = Model.Chunk<ScoreProcessQueue>($"status = 1 and mode = {mode}", AppSettings.ChunkSize);
                                foreach (var chunk in chunks)
                                {
                                    var scoreIds = chunk.Select(x => x.ScoreId).ToList();
                                    var scores = ScoreProcessQueue.FetchByScoreIds<T>(scoreIds).Where(x => x.ShouldIndex).ToList();
                                    var removedScores = scoreIds
                                        .Except(scores.Select(x => x.ScoreId))
                                        .Select(scoreId => new T { ScoreId = scoreId })
                                        .ToList();
                                    Console.WriteLine($"Got {chunk.Count} items from queue, found {scores.Count} matching scores, {removedScores.Count} missing scores");

                                    dispatcher.Enqueue(add: scores, remove: removedScores);
                                    ScoreProcessQueue.CompleteQueued<T>(scoreIds);
                                    count += scores.Count;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Rebuild from {resumeFrom}...");
                                var chunks = Model.Chunk<T>(AppSettings.ChunkSize, resumeFrom);
                                foreach (var chunk in chunks)
                                {
                                    dispatcher.Enqueue(chunk.Where(x => x.ShouldIndex).ToList());
                                    count += chunk.Count;
                                    // update resumeFrom in this scope to allow resuming from connection errors.
                                    resumeFrom = chunk.Last().CursorValue;
                                }
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
        }

        /// <summary>
        /// Attempts to find the matching index or creates a new one.
        /// </summary>
        /// <returns>Name of index found or created and any existing alias.</returns>
        private string findOrCreateIndex(string name)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Find or create index for `{name}`...");
            var metas = IndexMeta.GetByAlias(name).ToList();
            var aliasedIndices = elasticClient.GetIndicesPointingToAlias(name);
            string index;

            if (!AppSettings.IsNew)
            {
                // TODO: query ES that the index actually exists.

                index = metas.FirstOrDefault(m => aliasedIndices.Contains(m.Index))?.Index;
                // 3 cases are handled:
                // 1. Index was already aliased and has tracking information; likely resuming from a completed job.
                if (index != null)
                {
                    Console.WriteLine($"Found matching aliased index `{index}`.");
                    return index;
                }

                // 2. Index has not been aliased and has tracking information; likely resuming from an incomplete job.
                index = metas.FirstOrDefault()?.Index;
                if (index != null)
                {
                    Console.WriteLine($"Found previous index `{index}`.");
                    return index;
                }
            }

            // 3. Not aliased and no tracking information; likely starting from scratch
            var suffix = Suffix ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            index = $"{name}_{suffix}";

            Console.WriteLine($"Creating `{index}` for `{name}`.");
            // create by supplying the json file instead of the attributed class because we're not
            // mapping every field but still want everything for _source.
            var json = File.ReadAllText(Path.GetFullPath("schemas/high_scores.json"));
            elasticClient.LowLevel.IndicesCreate<DynamicResponse>(index, json);

            return index;

            // TODO: cases not covered should throw an Exception (aliased but not tracked, etc).
        }

        private IndexMeta initialize()
        {
            var index = findOrCreateIndex(Name);
            // look for any existing resume data.
            var indexMeta = IndexMeta.GetByName(index) ?? new IndexMeta
            {
                Alias = Name,
                Index = index,
            };

            indexMeta.LastId = ResumeFrom ?? indexMeta.LastId;

            Console.WriteLine();
            Console.WriteLine($"{typeof(T)}, index `{index}`, chunkSize `{AppSettings.ChunkSize}`, resume `{indexMeta.LastId}`");

            if (AppSettings.IsRebuild)
            {
                // Save the position the score processing queue should be reset to if rebuilding an index.
                // If there is already an existing value, the processor is probabaly resuming from a previous run,
                // so we likely want to preserve that value.
                if (!indexMeta.ResetQueueTo.HasValue)
                {
                    var mode = HighScore.GetRulesetId<T>();
                    indexMeta.ResetQueueTo = ScoreProcessQueue.GetLastProcessedQueueId(mode);
                }
            }
            else
            {
                // process queue reset if any.
                if (indexMeta.ResetQueueTo.HasValue)
                {
                    Console.WriteLine($"Resetting queue_id > {indexMeta.ResetQueueTo}");
                    ScoreProcessQueue.UnCompleteQueued<T>(indexMeta.ResetQueueTo.Value);
                    indexMeta.ResetQueueTo = null;
                }
            }

            indexMeta.UpdatedAt = DateTimeOffset.UtcNow;
            IndexMeta.UpdateAsync(indexMeta);
            IndexMeta.Refresh();

            Console.WriteLine();

            return indexMeta;
        }

        private void updateAlias(string alias, string index, bool close = true)
        {
            Console.WriteLine($"Updating `{alias}` alias to `{index}`...");

            var aliasDescriptor = new BulkAliasDescriptor();
            var oldIndices = elasticClient.GetIndicesPointingToAlias(alias);

            foreach (var oldIndex in oldIndices)
                aliasDescriptor.Remove(d => d.Alias(alias).Index(oldIndex));

            aliasDescriptor.Add(d => d.Alias(alias).Index(index));

            Console.WriteLine(elasticClient.Alias(aliasDescriptor));

            // cleanup
            if (!close) return;
            foreach (var toClose in oldIndices.Where(x => x != index))
            {
                Console.WriteLine($"Closing {toClose}");
                elasticClient.CloseIndex(toClose);
            }
        }
    }
}
