// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;

namespace osu.ElasticIndexer
{
    internal class BulkIndexingDispatcher<T> where T : HighScore
    {
        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        // Self-limiting read-ahead buffer to ensure
        // there is always data ready to be dispatched to Elasticsearch.
        private readonly BlockingCollection<List<T>> readBuffer = new BlockingCollection<List<T>>(AppSettings.BufferSize);

        private readonly string alias;
        private readonly string index;

        internal BulkIndexingDispatcher(string alias, string index)
        {
            this.alias = alias;
            this.index = index;
        }

        internal void Enqueue(List<T> list) => readBuffer.Add(list);
        internal void EnqueueEnd() => readBuffer.CompleteAdding();

        /// <summary>
        /// Reads a buffer and dispatches bulk index requests to Elasticsearch until the buffer
        /// is marked as complete.
        /// </summary>
        internal void Run()
        {
            // custom partitioner and options to prevent Parallel.ForEach from going out of control.
            var partitioner = Partitioner.Create(
                readBuffer.GetConsumingEnumerable(),
                EnumerablePartitionerOptions.NoBuffering // buffering causes spastic behaviour.
            );

            var options = new ParallelOptions { MaxDegreeOfParallelism = AppSettings.Concurrency };

            Parallel.ForEach(partitioner, options, chunk =>
            {
                bool success;

                while (true)
                {
                    var bulkDescriptor = new BulkDescriptor().Index(index).IndexMany(chunk);
                    var response = elasticClient.Bulk(bulkDescriptor);

                    bool retry;
                    (success, retry) = retryOnResponse(response, chunk);

                    if (!retry) break;

                    Task.Delay(AppSettings.BulkAllBackOffTimeDefault).Wait();
                }

                if (success && AppSettings.IsCrawler)
                {
                    // TODO: should probably aggregate responses and update to highest successful.
                    IndexMeta.UpdateAsync(new IndexMeta
                    {
                        Index = index,
                        Alias = alias,
                        LastId = chunk.Last().CursorValue,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            });

            IndexMeta.Refresh();
        }

        private (bool success, bool retry) retryOnResponse(IBulkResponse response, List<T> chunk)
        {
            // Elasticsearch bulk thread pool is full.
            if (response.ItemsWithErrors.Any(item => item.Status == 429 || item.Error.Type == "es_rejected_execution_exception"))
            {
                Console.WriteLine($"Server returned 429, re-queued chunk with lastId {chunk.Last().CursorValue}");
                return (success: false, retry: true);
            }

            // Index was closed, possibly because it was switched. Flag for bailout.
            if (response.ItemsWithErrors.Any(item => item.Error.Type == "index_closed_exception"))
            {
                Console.Error.WriteLine($"{index} was closed.");
                readBuffer.CompleteAdding(); // FIXME: should cancel instead.
                return (success: false, retry: false);
            }

            // TODO: other errors should do some kind of notification.
            return (success: true, retry: false);
        }
    }
}
