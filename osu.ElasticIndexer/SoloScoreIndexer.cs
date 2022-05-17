// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;

namespace osu.ElasticIndexer
{
    public class SoloScoreIndexer
    {
        // TODO: maybe have a fixed name?
        public string Name { get; set; } = IndexHelper.INDEX_NAME;
        private CancellationTokenSource? cts;
        private Metadata? metadata;
        private string? previousSchema;

        public void Run(CancellationToken token)
        {
            using (cts = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                metadata = IndexHelper.FindOrCreateIndex(Name);

                checkSchema();

                using (new Timer(_ => checkSchema(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5)))
                    new Processor(metadata.RealName).Run(cts.Token);
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
                return;

            // schema has changed to the current one
            if (previousSchema != schema && schema == AppSettings.Schema)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Schema switched to current: {schema}");
                Console.ResetColor();
                previousSchema = schema;
                IndexHelper.UpdateAlias(Name, metadata!.RealName);
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Previous schema {previousSchema}, got {schema}, need {AppSettings.Schema}, exiting...");
            Console.ResetColor();
            Stop();
        }
    }
}
