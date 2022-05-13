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
            try
            {
                getIndexer().Run();
            }
            catch (VersionMismatchException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("All schema versions mismatched, exiting.");
                Environment.Exit(0);
            }
        }

        private SoloScoreIndexer getIndexer()
        {
            var indexName = IndexHelper.INDEX_NAME;

            var indexer = new SoloScoreIndexer();
            indexer.Name = indexName;

            return indexer;
        }
    }
}
