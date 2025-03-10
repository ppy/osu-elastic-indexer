// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("alias", Description = "Updates alias to the latest index of a given version.")]
    public class UpdateAliasCommand
    {
        [Option("--close", Description = "Closes the previously aliased index when switching.")]
        public bool Close { get; set; }

        [Argument(0, "schema", "The schema version to alias.")]
        [Required]
        public string Schema { get; set; } = string.Empty;

        private readonly ConnectionMultiplexer redis = RedisAccess.GetConnection();
        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        public int OnExecute(CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(Schema))
            {
                Console.WriteLine("A schema version is required.");
                return 1;
            }

            var index = elasticClient.GetIndexForSchema(Schema);

            if (index == null)
            {
                Console.WriteLine("No matching indices found.");
                return 1;
            }

            // TODO: should check if completed?
            string? indexName = index.Value.Key.Name;

            elasticClient.UpdateAlias(AppSettings.AliasName, indexName, Close);

            redis.SetCurrentSchema(indexName);

            return ListIndicesCommand.ListSchemas(redis, elasticClient);
        }
    }
}
