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

        private readonly OsuElasticClient elasticClient;
        private readonly string index;

        // QueueProcessor doesn't expose cancellation without overriding Run,
        // so we're making use of a supplied callback to stop processing.
        private readonly Action stop;

        internal IndexQueueProcessor(string index, OsuElasticClient elasticClient, Action stopCallback)
            : base(new QueueConfiguration
            {
                InputQueueName = queue_name,
                BatchSize = AppSettings.BatchSize,
                ErrorThreshold = AppSettings.BatchSize * 2, // needs to be larger than BatchSize to handle ES busy errors.
                MaxInFlightItems = AppSettings.BatchSize * AppSettings.BufferSize
            })
        {
            this.elasticClient = elasticClient;
            this.index = index;
            stop = stopCallback;

            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();
        }

        protected override void ProcessResults(IEnumerable<ScoreItem> items)
        {
            ProcessableItemsBuffer buffer;

            using (var conn = GetDatabaseConnection())
                buffer = new ProcessableItemsBuffer(conn, items);

            if (buffer.Additions.Any() || buffer.Deletions.Any())
            {
                var bulkDescriptor = new BulkDescriptor()
                                     // Disabling exceptions streamlines error handling; all the relevant info will be in the response.
                                     // With exceptions, some of the source data gets lost when it's put into the exception message.
                                     .RequestConfiguration(r => r.ThrowExceptions(false))
                                     .Index(index)
                                     .IndexMany(buffer.Additions)
                                     // type is needed for ids https://github.com/elastic/elasticsearch-net/issues/3500
                                     .DeleteMany<SoloScore>(buffer.Deletions);

                var response = elasticClient.Bulk(bulkDescriptor);
                handleResponse(response, items);
            }
        }

        private void handleResponse(BulkResponse response, IEnumerable<ScoreItem> items)
        {
            if (response.IsValid)
            {
                // everything is fine (until they change how IsValid works).
                return;
            }

            // Just assume everything failed.
            foreach (var item in items)
            {
                item.Failed = true;
            }

            // Index was closed, possibly because it was switched. Flag for bailout.
            if (response.ItemsWithErrors.Any(item => item.Error.Type == "index_closed_exception" || item.Error.Type == "cluster_block_exception"))
            {
                Console.WriteLine(ConsoleColor.Red, $"{index} was closed.");
                stop();
                return;
            }

            // Try to figure out what's wrong
            var error = response.ServerError;

            if (error == null)
            {
                // TODO: requeue without marking as failed if server is offline.

                // Something caused an error without a ServerError to be set;
                // the server may be offline or unable to set ServerError.
                // ...or an index error that didn't cause a server error; either way, we can't handle it.

                Console.WriteLine(ConsoleColor.Red, response.ToString());
                Console.WriteLine(ConsoleColor.Red, response.OriginalException?.ToString());
                // Items may or may not have been set; try to get any information out.
                Console.WriteLine(ConsoleColor.Yellow, response.ItemsWithErrors.FirstOrDefault()?.Error.ToString());

                var exceptionTypeString = response.OriginalException?.GetType().ToString() ?? "null";

                DogStatsd.Increment("server_error", 1, 1, new[] { "status:unknown", $"exception_type:{exceptionTypeString}" });
                stop();

                return;
            }

            Console.WriteLine(ConsoleColor.Red, error.Error.Reason);
            // es_rejected_execution_exception is the server is too busy.
            // circuit_breaking_exception is a sign the jvm heap is too small or GC is stalling.
            DogStatsd.Increment("server_error", 1, 1, new[] { $"status:{error.Status}", $"type:{error.Error.Type}" });

            if (error.Status == 429)
            {
                Console.WriteLine(ConsoleColor.Yellow, $"Delaying chunk with lastId {items.Last()}");
                // TODO: need a better way to tell queue to slow down (this delays requeuing and also exiting).
                Task.Delay(AppSettings.BulkAllBackOffTimeDefault).Wait();
            }

            // TODO: per-item errors?
        }
    }
}
