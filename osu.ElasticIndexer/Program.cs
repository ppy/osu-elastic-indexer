// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Threading;
using System.Linq;
using Dapper;
using MySql.Data.MySqlClient;

namespace osu.ElasticIndexer
{
    public class Program
    {
        public static void Main()
        {
            if (AppSettings.UseDocker)
            {
                Console.WriteLine("Waiting for database...");

                while (true)
                {
                    try
                    {
                        using (var conn = new MySqlConnection(AppSettings.ConnectionString))
                        {
                            if (conn.QuerySingle<int>("SELECT `count` FROM `osu_counts` WHERE `name` = 'docker_db_step'") >= 3)
                                break;
                        }
                    }
                    catch
                    {
                    }

                    Thread.Sleep(1000);
                }
            }

            DefaultTypeMap.MatchNamesWithUnderscores = true;
            IndexMeta.CreateIndex();

            Console.WriteLine($"Rebuilding index: `{AppSettings.IsRebuild}`");
            if (AppSettings.IsWatching)
                runWatchLoop();
            else
                runIndexing();

            if (AppSettings.UseDocker)
            {
                using (var conn = new MySqlConnection(AppSettings.ConnectionString))
                {
                    conn.Execute("INSERT INTO `osu_counts` (`name`, `count`) VALUES (@Name, @Count) ON DUPLICATE KEY UPDATE `count` = @Count", new
                    {
                        Name = "docker_db_step",
                        Count = 4
                    });
                }
            }
        }

        // ReSharper disable once FunctionNeverReturns
        private static void runWatchLoop()
        {
            Console.WriteLine($"Running in watch mode with {AppSettings.PollingInterval}ms poll.");

            while (true)
            {
                // run continuously with automatic resume logic
                runIndexing();
                Thread.Sleep(AppSettings.PollingInterval);
            }
        }

        /// <summary>
        /// Performs a single indexing run for all specified modes.
        /// </summary>
        private static void runIndexing()
        {
            var suffix = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            foreach (var mode in AppSettings.Modes)
            {
                var indexer = getIndexerFromModeString(mode);
                indexer.Suffix = suffix;
                indexer.ResumeFrom = AppSettings.ResumeFrom;
                indexer.Run();
            }
        }

        private static IIndexer getIndexerFromModeString(string mode)
        {
            var indexName = $"{AppSettings.Prefix}high_scores_{mode}";
            var scoreType = getTypeFromModeString(mode);

            Type indexerType = typeof(HighScoreIndexer<>).MakeGenericType(scoreType);

            var indexer = (IIndexer)Activator.CreateInstance(indexerType);

            indexer.Name = indexName;
            indexer.IndexCompleted += (sender, args) =>
            {
                Console.WriteLine($"{args.Count} records took {args.TimeTaken}");
                if (args.Count > 0) Console.WriteLine($"{args.Count / args.TimeTaken.TotalSeconds} records/s");
            };

            return indexer;
        }

        private static Type getTypeFromModeString(string mode)
        {
            var className = $"{typeof(HighScore).Namespace}.HighScore{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(mode)}";

            return Type.GetType(className, true);
        }
    }
}
