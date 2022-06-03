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
        private Metadata? metadata;
        private string? previousSchema;
        private readonly Redis redis = new Redis();

        public SoloScoreIndexer()
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();
        }

        public void Run(CancellationToken token)
        {
            using (cts = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                metadata = client.FindOrCreateIndex(client.AliasName);

                checkSchema();

                using (new Timer(_ => checkSchema(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5)))
                    new Processor(metadata.RealName, client, Stop).Run(cts.Token);
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
                ConsoleColor.Yellow.WriteLine($"Schema switched to current: {schema}");
                previousSchema = schema;
                client.UpdateAlias(client.AliasName, metadata!.RealName);
                return;
            }

            ConsoleColor.Yellow.WriteLine($"Previous schema {previousSchema}, got {schema}, need {AppSettings.Schema}, exiting...");
            Stop();
        }
    }
}
