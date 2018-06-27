// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nest;

namespace osu.ElasticIndexer
{
    class BulkIndexingDispatcher<T> where T : Model
    {
        // ElasticClient is thread-safe and should be shared per host.
        private static readonly ElasticClient elasticClient = new ElasticClient
        (
            new ConnectionSettings(new Uri(AppSettings.ElasticsearchHost))
        );

        // BlockingCollection queues serve as a self-limiting read-ahead buffer to ensure
        // there is always data ready to be dispatched to Elasticsearch.
        private readonly BlockingCollection<List<T>> readBuffer = new BlockingCollection<List<T>>(AppSettings.QueueSize);

        // throttle control for adding delay on backpressure from the server.
        private int delay;

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

            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };

            Parallel.ForEach(partitioner, options, (chunk) =>
            {
                bool retry = true;
                bool success = false;

                while (retry)
                {
                    throttledWait();

                    var bulkDescriptor = new BulkDescriptor().Index(index).IndexMany(chunk);
                    var response = elasticClient.Bulk(bulkDescriptor);
                    (success, retry) = retryOnResponse(response, chunk);

                    unthrottle();
                    if (!retry) break;
                }

                if (success)
                {
                    // TODO: should probably aggregate responses and update to highest successful.
                    IndexMeta.Update(new IndexMeta
                    {
                        Index = index,
                        Alias = alias,
                        LastId = chunk.Last().CursorValue,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            });
        }

        private (bool success, bool retry) retryOnResponse(IBulkResponse response, List<T> chunk)
        {
            // Elasticsearch bulk thread pool is full.
            if (response.ItemsWithErrors.Any(item => item.Status == 429 || item.Error.Type == "es_rejected_execution_exception"))
            {
                Interlocked.Increment(ref delay);
                Console.WriteLine($"Server returned 429, requeued chunk with lastId {chunk.Last().CursorValue}");
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

        /// <summary>
        /// Slows down when the server is busy or too many requests are in-flight.
        /// </summary>
        private void throttledWait()
        {
            if (delay > 0) Task.Delay(delay * 100).Wait();
        }

        /// <summary>
        /// Gradually reduces the delay time between requests.
        /// </summary>
        private void unthrottle()
        {
            if (delay > 0) Interlocked.Decrement(ref delay);
        }
    }
}
