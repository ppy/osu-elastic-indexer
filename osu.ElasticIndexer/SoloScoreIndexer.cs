// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
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
        private Metadata? metadata;
        private string? previousSchema;

        public void Run()
        {
            metadata = IndexHelper.FindOrCreateIndex(Name);

            checkSchema();

            using (new Timer(_ => checkSchema(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5)))
            {
                new Processor(metadata.RealName).Run(cts.Token);
            }

            Console.WriteLine("Indexer stopped.");
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
            Stop();
        }

        public void Stop()
        {
            cts.Cancel();
        }

        public void Dispose()
        {
            cts.Dispose();
        }
    }
}
