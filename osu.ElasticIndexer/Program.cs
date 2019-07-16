// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Threading;
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

            Console.WriteLine($"Using queue: `{AppSettings.IsUsingQueue}`");
            if (AppSettings.IsWatching)
                runWatchLoop();
            else
            {
                runIndexing(AppSettings.ResumeFrom);
            }

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

            // run once with config resuming
            runIndexing(AppSettings.ResumeFrom);

            while (true)
            {
                // run continuously with automatic resume logic
                runIndexing(null);
                Thread.Sleep(AppSettings.PollingInterval);
            }
        }

        /// <summary>
        /// Performs a single indexing run for all specified modes.
        /// </summary>
        /// <param name="resumeFrom">An optional resume point.</param>
        private static void runIndexing(ulong? resumeFrom)
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
