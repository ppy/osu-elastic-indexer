// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class IndexQueueProcessor : QueueProcessor<ScoreItem>
    {
        private static readonly string queue_name = $"score-index-{AppSettings.Schema}";

        private readonly Client client;
        private readonly string index;

        // QueueProcessor doesn't expose cancellation without overriding Run,
        // so we're making use of a supplied callback to stop processing.
        private readonly Action stop;

        internal IndexQueueProcessor(string index, Client client, Action stopCallback)
            : base(new QueueConfiguration
            {
                InputQueueName = queue_name,
                BatchSize = AppSettings.BatchSize,
                ErrorThreshold = AppSettings.BatchSize * 2, // needs to be larger than BatchSize to handle ES busy errors.
                MaxInFlightItems = AppSettings.BatchSize * AppSettings.BufferSize
            })
        {
            this.client = client;
            this.index = index;
            stop = stopCallback;
        }

        protected override void ProcessResults(IEnumerable<ScoreItem> items)
        {
            var buffer = new IndexQueueItems();

            // Figure out what to do with the queue item.
            foreach (var item in items)
            {
                var action = item.ParsedAction;

                if (item.ScoreId != null && action != null)
                {
                    var id = (long)item.ScoreId; // doesn't figure out id isn't nullable here...

                    if (action == "delete")
                    {
                        buffer.Remove.Add(id.ToString());
                    }
                    else if (action == "index")
                    {
                        buffer.LookupIds.Add(id);
                    }
                }
                else if (item.Score != null)
                {
                    addToBuffer(item.Score, buffer);
                }
                else
                {
                    Console.WriteLine(ConsoleColor.Red, "queue item missing both data and action");
                }
            }

            // Handle any scores that need a lookup.
            performLookup(buffer);

            if (buffer.Add.Any() || buffer.Remove.Any())
            {
                var bulkDescriptor = new BulkDescriptor()
                                     .Index(index)
                                     .IndexMany(buffer.Add)
                                     // type is needed for string ids https://github.com/elastic/elasticsearch-net/issues/3500
                                     .DeleteMany<SoloScore>(buffer.Remove);

                var response = client.ElasticClient.Bulk(bulkDescriptor);

                handleResponse(response, items);
            }
        }

        private void addToBuffer(SoloScore score, IndexQueueItems buffer)
        {
            if (score.ShouldIndex)
                buffer.Add.Add(score);
            else
                buffer.Remove.Add(score.id.ToString());
        }

        private BulkResponse dispatch(BulkDescriptor bulkDescriptor)
        {
            try
            {
                return client.ElasticClient.Bulk(bulkDescriptor);
            }
            catch (ElasticsearchClientException ex)
            {
                // Server disappeared, maybe network failure or it's restarting; spin until it's available again.
                Console.WriteLine(ConsoleColor.Red, ex.Message);
                Console.WriteLine(ConsoleColor.Yellow, ex.InnerException?.Message);
                waitUntilActive();

                return dispatch(bulkDescriptor);
            }
        }

        private void handleResponse(BulkResponse response, IEnumerable<ScoreItem> items)
        {
            if (!response.IsValid)
            {
                // If it gets to here, then something is really wrong, just bail out.
                Console.WriteLine(ConsoleColor.Red, response.ToString());
                Console.WriteLine(ConsoleColor.Red, response.OriginalException?.Message);

                foreach (var item in items)
                {
                    item.Failed = true;
                }

                stop();
            }

            // Elasticsearch bulk thread pool is full.
            if (response.ItemsWithErrors.Any(item => item.Status == 429 || item.Error.Type == "es_rejected_execution_exception"))
            {
                Console.WriteLine(ConsoleColor.Yellow, $"Server returned 429, re-queued chunk with lastId {items.Last()}");

                foreach (var item in items)
                {
                    item.Failed = true;
                }

                // TODO: need a better way to tell queue to slow down (this delays requeuing and also exiting).
                Task.Delay(AppSettings.BulkAllBackOffTimeDefault).Wait();
                return;
            }

            // Index was closed, possibly because it was switched. Flag for bailout.
            if (response.ItemsWithErrors.Any(item => item.Error.Type == "index_closed_exception"))
            {
                Console.WriteLine(ConsoleColor.Red, $"{index} was closed.");

                // requeue in case it was an accident.
                foreach (var item in items)
                {
                    item.Failed = true;
                }

                stop();
            }

            // TODO: per-item errors?
        }

        private void performLookup(IndexQueueItems buffer)
        {
            if (!buffer.LookupIds.Any()) return;

            var scores = ElasticModel.Find<SoloScore>(buffer.LookupIds);

            foreach (var score in scores)
            {
                addToBuffer(score, buffer);
                buffer.LookupIds.Remove(score.id);
            }

            // Remaining scores do not exist and should be deleted.
            buffer.Remove.AddRange(buffer.LookupIds.Select(id => id.ToString()));

            buffer.LookupIds.Clear();
        }

        private void waitUntilActive()
        {
            // Spin until valid response from elasticsearch.
            while (!client.ElasticClient.Indices.Get(index, d => d.RequestConfiguration(r => r.ThrowExceptions(false))).IsValid)
            {
                Console.WriteLine(ConsoleColor.Yellow, "wating 10 seconds for server to come alive...");
                Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            }
        }
    }
}
