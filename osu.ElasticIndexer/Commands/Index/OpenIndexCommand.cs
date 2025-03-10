// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("open", Description = "Opens an index.")]
    public class OpenIndexCommand
    {
        [Argument(0, "name", "The index to open.")]
        [Required]
        public string Name { get; } = string.Empty;

        private readonly ConnectionMultiplexer redis = RedisAccess.GetConnection();
        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        public int OnExecute(CancellationToken token)
        {
            Console.WriteLine($"Opening {Name}..");
            var response = elasticClient.Indices.Open(Name);

            if (!response.IsValid)
            {
                Console.WriteLine($"Error: {response.ServerError}");
                return -1;
            }

            Console.WriteLine($"Adding {Name} to active schemas..");
            RedisAccess.GetConnection().AddActiveSchema(Name);

            return ListIndicesCommand.ListSchemas(redis, elasticClient);
        }
    }
}
