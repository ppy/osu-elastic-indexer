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

        public ulong? FirstPendingQueueId { get; set; }
        public string Name { get; set; }
        public ulong? ResumeFrom { get; set; }
        public string Suffix { get; set; }

        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        private BulkIndexingDispatcher<T> dispatcher;

        public void Run()
        {
            var index = findOrCreateIndex(Name);
            // find out if we should be resuming; could be resuming from a previously aborted run,
            // so don't assume the presence of a value means completion.
            var indexMeta = IndexMeta.GetByName(index);
            var resumeFrom = ResumeFrom ?? indexMeta?.LastId ?? 0;
            if (indexMeta.ResetQueueTo.HasValue)
                ScoreProcessQueue.UnCompleteQueued<T>(indexMeta.ResetQueueTo.Value);

            Console.WriteLine();
            Console.WriteLine($"{typeof(T)}, index `{index}`, chunkSize `{AppSettings.ChunkSize}`, resume `{resumeFrom}`");
            Console.WriteLine();

            var indexCompletedArgs = new IndexCompletedArgs
            {
                Alias = Name,
                Index = index,
                StartedAt = DateTime.Now
            };

            dispatcher = new BulkIndexingDispatcher<T>(Name, index);

            try
            {
                var readerTask = databaseReaderTask(resumeFrom);
                dispatcher.Run();
                readerTask.Wait();

                // set queue reset position
                if (AppSettings.IsRebuild)
                {
                    indexMeta = IndexMeta.GetByName(index);
                    indexMeta.ResetQueueTo = FirstPendingQueueId;
                    IndexMeta.UpdateAsync(indexMeta);
                    IndexMeta.Refresh();
                }

                indexCompletedArgs.Count = readerTask.Result;
                indexCompletedArgs.CompletedAt = DateTime.Now;

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
                            if (AppSettings.IsUsingQueue)
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
                                Console.WriteLine("Crawling records...");
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
