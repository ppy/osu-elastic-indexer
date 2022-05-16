// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("queue", Description = "Watches queue and dispatches scores for indexing")]
    public class WatchQueue
    {
        public int OnExecute(CancellationToken token)
        {
            boot();
            runIndexing();
            return 0;
        }

        private void boot()
        {
            var schema = Helpers.GetSchemaVersion();

            if (string.IsNullOrEmpty(schema))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"No existing schema version set, setting to {AppSettings.Schema}");
                Console.ResetColor();
                Helpers.SetSchemaVersion(AppSettings.Schema);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Running queue with schema version {AppSettings.Schema}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Performs a single indexing run for all specified modes.
        /// </summary>
        private void runIndexing()
        {
            using (var indexer = new SoloScoreIndexer())
            {
                Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs args) => indexer.Stop();

                var indexName = IndexHelper.INDEX_NAME;
                indexer.Name = indexName;
                indexer.Run();
            }
        }
    }
}
