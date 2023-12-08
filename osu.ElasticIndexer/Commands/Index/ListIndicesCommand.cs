// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Elasticsearch.Net;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("list", Description = "Lists indices.")]
    public class ListIndicesCommand
    {
        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        private readonly ConnectionMultiplexer redis = RedisAccess.GetConnection();

        public int OnExecute()
        {
            string[] activeSchemas = redis.GetActiveSchemas();
            string currentSchema = redis.GetCurrentSchema();

            var indices = elasticClient.GetIndices(elasticClient.AliasName, ExpandWildcards.All);

            Console.WriteLine("# Redis tracking");
            Console.WriteLine();

            Console.WriteLine($"Active schemas: {string.Join(',', activeSchemas)}");
            Console.WriteLine($"Current schema: {currentSchema}");
            Console.WriteLine();

            Console.WriteLine("# Elasticsearch indices");
            Console.WriteLine();

            if (indices.Count > 0)
            {
                var response = elasticClient.Cat.Indices(descriptor => descriptor.Index(indices.Keys.Select(k => k.Name).ToArray()));

                foreach (var record in response.Records)
                {
                    var indexState = indices[record.Index];
                    var schema = indexState.Mappings.Meta?["schema"];
                    var aliased = indexState.Aliases.ContainsKey(elasticClient.AliasName);

                    Console.WriteLine($"{record.Index} ({record.PrimaryStoreSize})\n"
                                      + $"- schema version: {schema}\n"
                                      + $"- aliased: {aliased}\n"
                                      + $"- status: {record.Status}\n"
                                      + $"- docs: {record.DocsCount} ({record.DocsDeleted} deleted)\n");
                }
            }

            Console.WriteLine();

            return 0;
        }
    }
}
