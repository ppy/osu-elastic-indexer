// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;
using Indices = Nest.Indices;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("list", Description = "Lists indices.")]
    public class ListIndicesCommand
    {
        protected readonly OsuElasticClient ElasticClient = new OsuElasticClient();

        protected readonly ConnectionMultiplexer Redis = RedisAccess.GetConnection();

        public virtual int OnExecute(CancellationToken token)
        {
            string[] activeSchemas = Redis.GetActiveSchemas();
            string currentSchema = Redis.GetCurrentSchema();

            Console.WriteLine("# Redis tracking");
            Console.WriteLine();

            Console.WriteLine($"Active schemas: {string.Join(',', activeSchemas)}");
            Console.WriteLine($"Current schema: {currentSchema}");
            Console.WriteLine();

            var indices = ElasticClient.GetIndices($"{ElasticClient.AliasName}_*");
            var records = ElasticClient.Cat.Indices(descriptor => descriptor.Index(Indices.All)).Records;

            Console.WriteLine($"# Elasticsearch indices ({indices.Count})");
            Console.WriteLine();

            foreach (var index in indices)
            {
                var record = records.Single(r => r.Index == index.Key);

                Console.WriteLine($"{record.Index} ({record.PrimaryStoreSize})\n"
                                  + $"- aliases: {string.Join(',', index.Value.Aliases.Select(a => a.Key))}\n"
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

            if (!string.IsNullOrEmpty(currentSchema))
            {
                if (!indices.TryGetValue(currentSchema, out var currentIndex))
                {
                    Console.WriteLine(ConsoleColor.Red, "ERROR: Current schema is not in present on elasticsearch");
                    return -1;
                }

                if (!currentIndex.Aliases.ContainsKey(ElasticClient.AliasName))
                {
                    Console.WriteLine(ConsoleColor.Red, "ERROR: Current schema is not aliased correctly");
                    return -1;
                }
            }

            return 0;
        }
    }
}
