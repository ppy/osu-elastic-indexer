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
            runIndexing();
            return 0;
        }

        /// <summary>
        /// Performs a single indexing run for all specified modes.
        /// </summary>
        private static void runIndexing()
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

        private static SoloScoreIndexer getIndexer()
        {
            var indexName = IndexHelper.INDEX_NAME;

            var indexer = new SoloScoreIndexer();
            indexer.Name = indexName;

            return indexer;
        }
    }
}
