// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;
using System.Globalization;
using System.Threading;
using Dapper;

namespace osu.ElasticIndexer
{
    public class Program
    {
        public static void Main()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            if (AppSettings.IsWatching)
                Console.WriteLine($"Running in watch mode with {AppSettings.PollingInterval}ms poll.");

            bool ranOnce = false;

            while (!ranOnce || AppSettings.IsWatching)
            {
                // When running in watch mode, the indexer should be told to resume from the
                // last known saved point instead of the configured value.
                runIndexing(ranOnce ? null : AppSettings.ResumeFrom);
                ranOnce = true;
                if (AppSettings.IsWatching) Thread.Sleep(AppSettings.PollingInterval);
            }
        }

        private static void runIndexing(long? resumeFrom)
        {
            var suffix = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            foreach (var mode in AppSettings.Modes)
            {
                var indexer = getIndexerFromModeString(mode);
                indexer.Suffix = suffix;
                indexer.ResumeFrom = resumeFrom;
                indexer.Run();
            }
        }

        private static IIndexer getIndexerFromModeString(string mode)
        {
            var indexName = $"{AppSettings.Prefix}high_scores_{mode}";
            var className = $"{typeof(HighScore).Namespace}.HighScore{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(mode)}";

            Type indexerType = typeof(HighScoreIndexer<>).MakeGenericType(Type.GetType(className, true));

            var indexer = (IIndexer)Activator.CreateInstance(indexerType);

            indexer.Name = indexName;
            indexer.IndexCompleted += (sender, args) =>
            {
                Console.WriteLine($"{args.Count} records took {args.TimeTaken}");
                if (args.Count > 0) Console.WriteLine($"{args.Count / args.TimeTaken.TotalSeconds} records/s");
            };

            return indexer;
        }
    }
}
