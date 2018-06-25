// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest;

namespace osu.ElasticIndexer
{
    public class HighScoreIndexer<T> : IIndexer where T : Model
    {
        public event EventHandler<IndexCompletedArgs> IndexCompleted;

        public string Name { get; set; }
        public long? ResumeFrom { get; set; }
        public string Suffix { get; set; }

        // ElasticClient is thread-safe and should be shared per host.
        private static readonly ElasticClient elasticClient = new ElasticClient
        (
            new ConnectionSettings(new Uri(AppSettings.ElasticsearchHost))
        );

        private readonly ConcurrentBag<Task<IBulkResponse>> pendingTasks = new ConcurrentBag<Task<IBulkResponse>>();

        // BlockingCollection queues serve as a self-limiting read-ahead buffer to ensure
        // there is always data ready to be dispatched to Elasticsearch. The separate queue buffer for
        // retries allows retries to preempt the read buffer.
        private readonly BlockingCollection<List<T>> readBuffer = new BlockingCollection<List<T>>(AppSettings.QueueSize);
        private readonly BlockingCollection<List<T>> retryBuffer = new BlockingCollection<List<T>>(AppSettings.QueueSize);
        private readonly BlockingCollection<List<T>>[] queues;

        // throttle control; Gracefully handles busy signals from the server
        // by gruadually increasing the delay on busy signals,
        // while decreasing  as dispatches complete.
        private int delay;

        public HighScoreIndexer()
        {
            queues = new [] { retryBuffer, readBuffer };
        }

        public void Run()
        {
            var index = findOrCreateIndex(Name);
            // find out if we should be resuming
            var resumeFrom = ResumeFrom ?? IndexMeta.GetByName(index)?.LastId;

            Console.WriteLine();
            Console.WriteLine($"{typeof(T)}, index `{index}`, chunkSize `{AppSettings.ChunkSize}`, resume `{resumeFrom}`");
            Console.WriteLine();

            var indexCompletedArgs = new IndexCompletedArgs
            {
                Alias = Name,
                Index = index,
                StartedAt = DateTime.Now
            };

            using (var dispatcherTask = elasticsearchDispatcherTask(index))
            using (var readerTask = databaseReaderTask(resumeFrom))
            {
                readerTask.Wait();
                prepareToShutdown();
                dispatcherTask.Wait();

                indexCompletedArgs.Count = readerTask.Result;
                indexCompletedArgs.CompletedAt = DateTime.Now;
            }

            updateAlias(Name, index);
            IndexCompleted(this, indexCompletedArgs);
        }

        /// <summary>
        /// Self contained database reader task. Reads the database by cursoring through records
        /// and adding chunks into readBuffer.
        /// </summary>
        /// <param name="resumeFrom">The cursor value to resume from;
        /// use null to resume from the last known value.</param>
        /// <returns>The database reader task.</returns>
        private Task<long> databaseReaderTask(long? resumeFrom)
        {
            return Task.Factory.StartNew(() =>
            {
                long count = 0;

                while (true)
                {
                    try
                    {
                        var chunks = Model.Chunk<T>(AppSettings.ChunkSize, resumeFrom);
                        foreach (var chunk in chunks)
                        {
                            readBuffer.Add(chunk);
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

                readBuffer.CompleteAdding();
                Console.WriteLine($"Finished reading database {count} records.");

                return count;
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
        }

        /// <summary>
        /// Creates a task that loops and takes items from readBuffer until completion,
        /// dispatching them to Elasticsearch for indexing.
        /// </summary>
        /// <param name="index">The name of the index to dispatch to.</param>
        /// <returns>The dispatcher task.</returns>
        private Task elasticsearchDispatcherTask(string index)
        {
            return Task.Factory.StartNew(() =>
            {
                while (!readBuffer.IsCompleted || !retryBuffer.IsCompleted)
                {
                    throttledWait();

                    List<T> chunk;

                    try
                    {
                        BlockingCollection<List<T>>.TakeFromAny(queues, out chunk);
                    }
                    catch (ArgumentException ex)
                    {
                        // queue was marked as completed while blocked.
                        Console.WriteLine(ex.Message);
                        continue;
                    }

                    var bulkDescriptor = new BulkDescriptor().Index(index).IndexMany(chunk);
                    Task<IBulkResponse> task = elasticClient.BulkAsync(bulkDescriptor);
                    pendingTasks.Add(task);

                    task.ContinueWith(t =>
                    {
                        handleResult(t.Result, chunk);
                        pendingTasks.TryTake(out t);
                    });

                    // TODO: Less blind-fire update.
                    // I feel like this is in the wrong place...
                    IndexMeta.Update(new IndexMeta
                    {
                        Index = index,
                        Alias = Name,
                        LastId = chunk.Last().CursorValue,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });

                    unthrottle();
                }
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
        }

        private void handleResult(IBulkResponse response, List<T> chunk)
        {
            if (response.ItemsWithErrors.All(item => item.Status != 429)) return;

            Interlocked.Increment(ref delay);
            retryBuffer.Add(chunk);

            Console.WriteLine($"Server returned 429, requeued chunk with lastId {chunk.Last().CursorValue}");
        }

        /// <summary>
        /// Prepares the instance for stopping by waiting for buffers to drain.
        /// </summary>
        private void prepareToShutdown()
        {
            Console.WriteLine($@"Draining buffers...");
            while (pendingTasks.Count + readBuffer.Count + retryBuffer.Count > 0) {
                Task.Delay(100).Wait();
                Task.WhenAll(pendingTasks).Wait();
            }

            retryBuffer.CompleteAdding();
        }

        private void throttledWait()
        {
            if (delay > 0) Task.Delay(delay * 100).Wait();

            // too many pending responses, wait and let them be handled.
            if (pendingTasks.Count > AppSettings.QueueSize * 2) {
                Console.WriteLine($"Too many pending responses ({pendingTasks.Count}), waiting...");
                pendingTasks.FirstOrDefault()?.Wait();
            }
        }

        private void unthrottle()
        {
            if (delay > 0) Interlocked.Decrement(ref delay);
        }

        /// <summary>
        /// Attemps to find the matching index or creates a new one.
        /// </summary>
        /// <param name="name">Name of the alias to find the matching index for.</param>
        /// <returns>Name of index found or created.</returns>
        private string findOrCreateIndex(string name)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Find or create index for `{name}`...");
            var metas = IndexMeta.GetByAlias(name).ToList();
            var indices = elasticClient.GetIndicesPointingToAlias(name);
            string index;

            if (!AppSettings.IsNew)
            {
                index = metas.FirstOrDefault(m => indices.Contains(m.Index))?.Index;
                // 3 cases are handled:
                // 1. Index was already aliased and has tracking information; likely resuming from a completed job.
                if (index != null)
                {
                    Console.WriteLine($"Found matching aliased index `{index}`.");
                    return index;
                }

                // 2. Index has not been aliased and has tracking information; likely resuming from an imcomplete job.
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

        private void updateAlias(string alias, string index)
        {
            Console.WriteLine($"Updating `{alias}` alias to `{index}`...");

            var aliasDescriptor = new BulkAliasDescriptor();
            var oldIndices = elasticClient.GetIndicesPointingToAlias(alias);

            foreach (var oldIndex in oldIndices)
                aliasDescriptor.Remove(d => d.Alias(alias).Index(oldIndex));

            aliasDescriptor.Add(d => d.Alias(alias).Index(index));

            Console.WriteLine(elasticClient.Alias(aliasDescriptor));

            // TODO: cleanup unaliased indices.
        }
    }
}
