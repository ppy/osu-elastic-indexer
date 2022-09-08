// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;

namespace osu.ElasticIndexer
{
    public class SoloScoreIndexer
    {
        private readonly Client client = new Client();
        private CancellationTokenSource? cts;
        private IndexMetadata? metadata;
        private string? previousSchema;
        private readonly Redis redis = new Redis();

        public SoloScoreIndexer()
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();
        }

        public void Run(CancellationToken token, bool forceVersion = false)
        {
            using (cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                metadata = client.FindOrCreateIndex(client.AliasName);

                checkSchema();

                redis.AddActiveSchema(AppSettings.Schema);

                if (forceVersion)
                    redis.SetSchemaVersion(AppSettings.Schema);

                using (new Timer(_ => checkSchema(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5)))
                    new IndexQueueProcessor(metadata.Name, client, Stop).Run(cts.Token);
            }
        }

        public void Stop()
        {
            if (cts == null || cts.IsCancellationRequested)
                return;

            cts.Cancel();
        }

        private void checkSchema()
        {
            var schema = redis.GetSchemaVersion();

            // first run
            if (previousSchema == null)
            {
                // TODO: maybe include index check if it's out of date?
                previousSchema = schema;
                return;
            }

            // no change
            if (previousSchema == schema)
                return;

            // schema has changed to the current one
            if (previousSchema != schema && schema == AppSettings.Schema)
            {
                Console.WriteLine(ConsoleColor.Yellow, $"Schema switched to current: {schema}");
                previousSchema = schema;
                client.UpdateAlias(client.AliasName, metadata!.Name);
                return;
            }

            Console.WriteLine(ConsoleColor.Yellow, $"Previous schema {previousSchema}, got {schema}, need {AppSettings.Schema}, exiting...");
            redis.RemoveActiveSchema(AppSettings.Schema);
            Stop();
        }
    }
}
