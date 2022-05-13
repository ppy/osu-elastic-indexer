// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nest;

namespace osu.ElasticIndexer
{
    public class SoloScoreIndexer : IDisposable
    {
        // TODO: maybe have a fixed name?
        public string Name { get; set; } = IndexHelper.INDEX_NAME;
        public long? ResumeFrom { get; set; }

        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private BulkIndexingDispatcher<SoloScore>? dispatcher;
        private Metadata? metadata;
        private string? previousSchema;

        public void Run()
        {
            metadata = IndexHelper.FindOrCreateIndex(Name);

            checkSchema();

            dispatcher = new BulkIndexingDispatcher<SoloScore>(metadata.RealName);

            try
            {
                // TODO: processor needs to check if index is closed instead of spinning

                // TODO: dispatcher should be the separate task?
                // or fix 0 length queue buffer on start?

                using (new Timer(_ => checkSchema(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5)))
                {
                    var queueTask = Task.Factory.StartNew(() =>
                        {
                            new Processor(dispatcher).Run(cts.Token);
                        });

                    // Run() should block.
                    dispatcher.Run();
                    // something caused the dispatcher to bail out, e.g. index closed.
                    Console.WriteLine("stopping indexer...");
                    stop();
                    // FIXME: better shutdown (currently queue processer throws exception).
                    queueTask.Wait();
                    Console.WriteLine("indexer stopped.");
                }
            }
            catch (AggregateException ae)
            {
                Console.WriteLine(ae);
                ae.Handle(handleAggregateException);
            }

            // Local function exception handler.
            bool handleAggregateException(Exception ex)
            {
                if (!(ex is InvalidOperationException)) return false;

                Console.Error.WriteLine(ex.Message);
                if (ex.InnerException != null)
                    Console.Error.WriteLine(ex.InnerException.Message);

                return true;
            }
        }

        private void checkSchema()
        {
            var schema = Helpers.GetSchemaVersion();
            // first run
            if (previousSchema == null)
            {
                // TODO: maybe include index check if it's out of date?
                previousSchema = schema;
                return;
            }

            // no change
            if (previousSchema == schema)
            {
                return;
            }

            // schema has changed to the current one
            if (previousSchema != schema && schema == AppSettings.Schema)
            {
                Console.WriteLine($"Schema switched to current: {schema}");
                previousSchema = schema;
                IndexHelper.UpdateAlias(Name, metadata!.RealName);
                return;
            }

            Console.WriteLine($"Previous schema {previousSchema}, got {schema}, need {AppSettings.Schema}, exiting...");
            stop();
        }

        private void stop()
        {
            cts.Cancel();
        }

        public void Dispose()
        {
            cts.Dispose();
        }
    }
}
