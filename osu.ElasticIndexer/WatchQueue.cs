// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using Dapper;
using McMaster.Extensions.CommandLineUtils;
using MySqlConnector;

#nullable enable

namespace osu.ElasticIndexer
{
    [Command("queue", Description = "Watches queue and dispatches scores for indexing")]
    public class WatchQueue
    {
        protected readonly Processor<SoloScore> Queue = new Processor<SoloScore>();

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
            var indexName = $"{AppSettings.Prefix}solo_scores";

            var indexer = new SoloScoreIndexer();
            indexer.Name = indexName;
            indexer.ResumeFrom = AppSettings.ResumeFrom;

            indexer.IndexCompleted += (sender, args) =>
            {
                if (args.Count > 0)
                {
                    Console.WriteLine($"Indexed {args.Count} records in {args.TimeTaken.TotalMilliseconds:F0}ms ({args.Count / args.TimeTaken.TotalSeconds:F0}/s)");
                }
            };

            return indexer;
        }
    }
}
