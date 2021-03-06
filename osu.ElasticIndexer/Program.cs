﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dapper;
using MySqlConnector;
using StatsdClient;

namespace osu.ElasticIndexer
{
    public class Program
    {
        public static void Main()
        {
            DogStatsd.Configure(new StatsdConfig
            {
                StatsdServerName = "127.0.0.1",
                Prefix = "elasticsearch.scores"
            });

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
                AppSettings.IsNew = false;

                Console.WriteLine($"Sleeping {AppSettings.PollingInterval}..");
                Console.WriteLine();

                Thread.Sleep(AppSettings.PollingInterval);
            }
        }

        /// <summary>
        /// Performs a single indexing run for all specified modes.
        /// </summary>
        private static void runIndexing()
        {
            var mismatched = new HashSet<string>();
            var suffix = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            foreach (var mode in AppSettings.Modes)
            {
                try
                {
                    var indexer = getIndexerFromModeString(mode);
                    indexer.Suffix = suffix;
                    indexer.ResumeFrom = AppSettings.ResumeFrom;
                    indexer.Run();
                }
                catch (VersionMismatchException ex)
                {
                    Console.WriteLine(ex.Message);
                    mismatched.Add(mode);
                }
            }

            // A switchover probably happened and this process has nothing to do, so just exit.
            if (AppSettings.Modes.ToHashSet().SetEquals(mismatched))
            {
                Console.Error.WriteLine("All schema versions mismatched, exiting.");
                Environment.Exit(0);
            }
        }

        private static IIndexer getIndexerFromModeString(string mode)
        {
            var indexName = $"{AppSettings.Prefix}high_scores_{mode}";
            var scoreType = HighScore.GetTypeFromModeString(mode);

            Type indexerType = typeof(HighScoreIndexer<>).MakeGenericType(scoreType);

            var indexer = (IIndexer)Activator.CreateInstance(indexerType);

            indexer.Name = indexName;
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
