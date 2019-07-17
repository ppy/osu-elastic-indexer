// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest;

namespace osu.ElasticIndexer
{
    public class HighScoreIndexer<T> : IIndexer where T : HighScore
    {
        public event EventHandler<IndexCompletedArgs> IndexCompleted = delegate { };

        public string Name { get; set; }
        public string Index { get; private set; }
        public ulong? ResumeFrom { get; set; }
        public string Suffix { get; set; }

        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        private BulkIndexingDispatcher<T> dispatcher;

        public void Run()
        {
            Index = findOrCreateIndex(Name);
            // find out if we should be resuming; could be resuming from a previously aborted run,
            // so don't assume the presence of a value means completion.
            var indexMeta = IndexMeta.GetByName(Index);
            var resumeFrom = ResumeFrom ?? indexMeta?.LastId ?? 0;
            indexMeta = processQueueReset(indexMeta);

            Console.WriteLine();
            Console.WriteLine($"{typeof(T)}, index `{Index}`, chunkSize `{AppSettings.ChunkSize}`, resume `{resumeFrom}`");
            Console.WriteLine();

            var indexCompletedArgs = new IndexCompletedArgs
            {
                Alias = Name,
                Index = Index,
                StartedAt = DateTime.Now
            };

            dispatcher = new BulkIndexingDispatcher<T>(Index);
            if (AppSettings.IsRebuild)
                dispatcher.BatchWithLastIdCompleted += handleBatchWithLastIdCompleted;

            try
            {
                var readerTask = databaseReaderTask(resumeFrom);
                dispatcher.Run();
                readerTask.Wait();

                indexCompletedArgs.Count = readerTask.Result;
                indexCompletedArgs.CompletedAt = DateTime.Now;

                IndexMeta.Refresh();
                updateAlias(Name, Index);
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
                    Index = Index,
                    Alias = Name,
                    LastId = lastId,
                    ResetQueueTo = indexMeta.ResetQueueTo,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
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
                                var mode = typeof(T).GetCustomAttributes<RulesetIdAttribute>().First().Id;
                                var chunks = Model.Chunk<ScoreProcessQueue>($"status = 1 and mode = {mode}", AppSettings.ChunkSize);
                                foreach (var chunk in chunks)
                                {
                                    var scoreIds = chunk.Select(x => x.ScoreId).ToList();
                                    var scores = ScoreProcessQueue.FetchByScoreIds<T>(scoreIds);
                                    var removedScores = scoreIds
                                        .Except(scores.Select(x => x.ScoreId))
                                        .Select(x => x.ToString())
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
                                    dispatcher.Enqueue(chunk);
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

        /// <summary>
        /// Saves the position the score processing queue should be reset to if rebuilding an index,
        /// resets the position of the queue to the saved position, otherwise.
        /// </summary>
        /// <param name="indexMeta">Document that contains the saved queue position.</param>
        private IndexMeta processQueueReset(IndexMeta indexMeta)
        {
            var copy = new IndexMeta
            {
                Alias = indexMeta?.Alias ?? Name,
                Index = indexMeta?.Index ?? Index,
                LastId = indexMeta?.LastId ?? 0,
                ResetQueueTo = indexMeta?.ResetQueueTo,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            if (AppSettings.IsRebuild)
            {
                // If there is already an existing value, the processor is probabaly resuming from a previous run,
                // so we likely want to preserve that value.
                if (!copy.ResetQueueTo.HasValue)
                {
                    var mode = typeof(T).GetCustomAttributes<RulesetIdAttribute>().First().Id;
                    copy.ResetQueueTo = ScoreProcessQueue.GetLastProcessedQueueId(mode);
                }
            }
            else
            {
                if (copy.ResetQueueTo.HasValue)
                {
                    ScoreProcessQueue.UnCompleteQueued<T>(copy.ResetQueueTo.Value);
                    copy.ResetQueueTo = null;
                }
            }

            IndexMeta.UpdateAsync(copy);
            IndexMeta.Refresh();

            return copy;
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
