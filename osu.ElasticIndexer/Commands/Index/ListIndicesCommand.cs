// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;
using Indices = Nest.Indices;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("list", Description = "Lists indices.")]
    public class ListIndicesCommand
    {
        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        private readonly ConnectionMultiplexer redis = RedisAccess.GetConnection();

        public virtual int OnExecute()
        {
            string[] activeSchemas = redis.GetActiveSchemas();
            string currentSchema = redis.GetCurrentSchema();

            Console.WriteLine("# Redis tracking");
            Console.WriteLine();

            Console.WriteLine($"Active schemas: {string.Join(',', activeSchemas)}");
            Console.WriteLine($"Current schema: {currentSchema}");
            Console.WriteLine();

            var indices = elasticClient.Indices.Get(Indices.All).Indices;
            var records = elasticClient.Cat.Indices(descriptor => descriptor.Index(Indices.All)).Records;

            Console.WriteLine($"# Elasticsearch indices ({indices.Count})");
            Console.WriteLine();

            foreach (var index in indices)
            {
                var schema = index.Value.Mappings.Meta?["schema"];

                var record = records.Single(r => r.Index == index.Key);

                Console.WriteLine($"{record.Index} ({record.PrimaryStoreSize})\n"
                                  + $"- schema version: {schema}\n"
                                  + $"- aliases: {string.Join(',', index.Value.Aliases)}\n"
                                  + $"- status: {record.Status}\n"
                                  + $"- docs: {record.DocsCount} ({record.DocsDeleted} deleted)\n");
            }

            if (string.IsNullOrEmpty(currentSchema))
                Console.WriteLine(ConsoleColor.Yellow, "WARNING: No current schema");
            if (activeSchemas.Length == 0)
                Console.WriteLine(ConsoleColor.Yellow, "WARNING: No active schemas");

            if (!activeSchemas.Contains(currentSchema))
            {
                Console.WriteLine(ConsoleColor.Red, "ERROR: Current schema is not in active schema list");
                return -1;
            }

            var currentIndex = indices.Select(i => i.Value).FirstOrDefault(i => (string?)i.Mappings.Meta?["schema"] == currentSchema);

            if (currentIndex == null)
            {
                Console.WriteLine(ConsoleColor.Red, "ERROR: Current schema is not in present on elasticsearch");
                return -1;
            }

            if (!currentIndex.Aliases.ContainsKey(elasticClient.AliasName))
            {
                Console.WriteLine(ConsoleColor.Red, "ERROR: Current schema is not aliased correctly");
                return -1;
            }

            return 0;
        }
    }
}
