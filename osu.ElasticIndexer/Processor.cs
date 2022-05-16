// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Nest;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class Processor : QueueProcessor<ScoreItem>
    {
        private static readonly string queueName = $"score-index-{AppSettings.Schema}";

        public string QueueName { get; private set; }

        private readonly BulkIndexingDispatcher dispatcher;
        private readonly string index;

        internal Processor(BulkIndexingDispatcher dispatcher, string index) : base(new QueueConfiguration { InputQueueName = queueName, BatchSize = AppSettings.ChunkSize, ErrorThreshold = 10000 })
        {
            this.index = index;
            this.dispatcher = dispatcher;
            QueueName = queueName;
        }

        protected override void ProcessResults(IEnumerable<ScoreItem> items)
        {
            Console.WriteLine($"{items.First().Score.Id} {items.Where(x => x.Failed).Count()}");
            var add = new List<SoloScore>();
            var remove = new List<SoloScore>();

            foreach (var item in items)
            {
                // TODO: have should index handled at reader?
                if (item.Score.ShouldIndex)
                    add.Add(item.Score);
                else
                    remove.Add(item.Score);
            }


            Console.WriteLine(add.First().Id);
            var bulkDescriptor = new BulkDescriptor()
                .Index(index)
                .IndexMany(add)
                .DeleteMany(remove);
            var response = AppSettings.ELASTIC_CLIENT.Bulk(bulkDescriptor);

            bool success;
            bool retry;
            (success, retry) = retryOnResponse(response, items);
            // dispatcher.Enqueue(add, remove);
        }

        private (bool success, bool retry) retryOnResponse(BulkResponse response, IEnumerable<ScoreItem> items)
        {
            Console.WriteLine(response.ItemsWithErrors.First().Id);
            // Elasticsearch bulk thread pool is full.
            if (response.ItemsWithErrors.Any(item => item.Status == 429 || item.Error.Type == "es_rejected_execution_exception"))
            {
                Console.WriteLine($"Server returned 429, re-queued chunk with lastId {items.Last().Score.Id}");
                foreach (var item in items)
                {
                    item.Failed = true;
                }

                return (success: false, retry: true);

            }
            // TODO: other errors should do some kind of notification.
            return (success: true, retry: false);
        }
    }
}
