// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("close", Description = "Closes unused indices.")]
    public class CloseIndexCommand
    {
        [Argument(0, "name", "The index to close. All unused indices are closed if not specified.")]
        public string? Name { get; set; }

        private readonly ConnectionMultiplexer redis = RedisAccess.GetConnection();
        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        public int OnExecute(CancellationToken token)
        {
            var indices = string.IsNullOrEmpty(Name) ? elasticClient.GetIndices($"{AppSettings.AliasName}_*") : elasticClient.GetIndex(Name);

            var closeableIndices = indices.Where(entry => entry.Value.Aliases.Count == 0);

            if (!closeableIndices.Any())
            {
                Console.WriteLine(ConsoleColor.Yellow, "No indices to close!");
                return 0;
            }

            Console.WriteLine("The following indices will be closed:");

            foreach (var entry in closeableIndices)
                Console.WriteLine(entry.Key.Name);

            Console.WriteLine();

            if (!Prompt.GetYesNo("Close these indices?", false, ConsoleColor.Yellow))
            {
                Console.WriteLine(ConsoleColor.Red, "aborted.");
                return 1;
            }

            Console.WriteLine();

            foreach (var entry in closeableIndices)
            {
                if (entry.Key.Name == redis.GetCurrentSchema())
                {
                    Console.WriteLine(ConsoleColor.Red, $"Index {entry.Key.Name} is set as current schema. Cannot close.");
                    return -1;
                }

                System.Console.WriteLine($"Removing {entry.Key.Name} from active schemas..");
                redis.RemoveActiveSchema(entry.Key.Name);

                Console.WriteLine($"Closing {entry.Key.Name}...");
                var response = elasticClient.Indices.Close(entry.Key.Name);

                if (!response.IsValid)
                {
                    Console.WriteLine($"Error: {response.ServerError}");
                    return -1;
                }
            }

            Console.WriteLine("done.");

            return ListIndicesCommand.ListSchemas(redis, elasticClient);
        }
    }
}
