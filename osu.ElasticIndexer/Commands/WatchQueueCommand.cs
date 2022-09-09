// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using Elasticsearch.Net;
using McMaster.Extensions.CommandLineUtils;
using StackExchange.Redis;
using MySqlConnector;

namespace osu.ElasticIndexer.Commands
{
    [Command("queue", Description = "Watches queue and dispatches scores for indexing")]
    public class WatchQueueCommand
    {
        [Option("--force-version", Description = "Forces the schema version in Redis to be this processor's version.")]
        public bool ForceVersion { get; set; }

        [Option("--wait", Description = "Waits for dependent services to start.")]
        public bool Wait { get; set; }

        public int OnExecute(CancellationToken token)
        {
            if (Wait)
                waitForServices();

            boot();
            new SoloScoreIndexer().Run(token, ForceVersion);
            return 0;
        }

        private void boot()
        {
            var schema = new Redis().GetSchemaVersion();

            if (string.IsNullOrEmpty(schema))
                Console.WriteLine(ConsoleColor.Yellow, "No existing schema version set, is this intended?");

            Console.WriteLine(ConsoleColor.Green, $"Running queue with schema version {AppSettings.Schema}");
        }

        private void waitForServices()
        {
            waitForElasticsearch();
            waitForRedis();
            // Database is last because we want to isolate the error handling but we have to get the
            // database connection string from QueueProcessor which also tries to connect to redis.
            waitForDatabase();
        }

        // Only waits for the database, doesn't wait for tables or migrations to complete.
        private void waitForDatabase()
        {
            while (true)
            {
                try
                {
                    // Can remove and use plain connection if https://github.com/ppy/osu-queue-processor/issues/13 is implemented.
                    new UnrunnableProcessor().GetDatabaseConnection().Dispose();
                    Console.WriteLine(ConsoleColor.Green, "Database is alive.");

                    break;
                }
                catch (MySqlException ex)
                {
                    // die on any other kind of error, e.g. permissions, etc.
                    if (ex.ErrorCode == MySqlErrorCode.UnableToConnectToHost || ex.ErrorCode == MySqlErrorCode.UnknownDatabase)
                        Thread.Sleep(10);
                    else
                        throw;
                }
            }
        }

        private void waitForElasticsearch()
        {
            var client = new Client(false);

            while (true)
            {
                try
                {
                    var response = client.ElasticClient.Cluster.Health();

                    // Yellow or Green cluster statuses are fine.
                    if (response.IsValid && response.Status != Health.Red)
                    {
                        Console.WriteLine(ConsoleColor.Green, $"Elasticsearch ({response.ClusterName}) is alive.");
                        break;
                    }
                }
                catch (UnexpectedElasticsearchClientException ex)
                {
                    // There is a period during elasticseasrch startup where active_shards_percent_as_number
                    // is returned as NaN but the parser expects a number.
                    if (!ex.Message.StartsWith("expected:'Number Token', actual:'\"NaN\"'"))
                        throw;
                }

                Thread.Sleep(10);
            }
        }

        private void waitForRedis()
        {
            while (true)
            {
                try
                {
                    var redis = new Redis();
                    redis.Connection.GetDatabase().Ping();
                    Console.WriteLine(ConsoleColor.Green, $"Redis ({redis.Connection.Configuration}) is alive.");

                    break;
                }
                catch (RedisConnectionException)
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}
