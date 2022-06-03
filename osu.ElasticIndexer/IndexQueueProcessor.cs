// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            var add = new List<SoloScore>();
            var remove = new List<SoloScore>();

            foreach (var item in items)
            {
                if (item.Score.ShouldIndex)
                    add.Add(item.Score);
                else
                    remove.Add(item.Score);
            }

            var bulkDescriptor = new BulkDescriptor()
                                 .Index(index)
                                 .IndexMany(add)
                                 .DeleteMany(remove);

            var response = client.ElasticClient.Bulk(bulkDescriptor);

            handleResponse(response, items);
        }

        private void handleResponse(BulkResponse response, IEnumerable<ScoreItem> items)
        {
            // Elasticsearch bulk thread pool is full.
            if (response.ItemsWithErrors.Any(item => item.Status == 429 || item.Error.Type == "es_rejected_execution_exception"))
            {
                Console.WriteLine(ConsoleColor.Yellow, $"Server returned 429, re-queued chunk with lastId {items.Last().Score.id}");

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
    }
}
