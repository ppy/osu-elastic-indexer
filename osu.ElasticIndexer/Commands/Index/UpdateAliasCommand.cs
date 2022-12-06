// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("alias", Description = "Updates alias to the latest index of a given version.")]
    public class UpdateAliasCommand
    {
        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        [Option("--close", Description = "Closes the previously aliased index when switching.")]
        public bool Close { get; set; }

        [Required]
        [Option("--schema", Description = "Required. The schema version.")]
        public string Schema { get; set; } = string.Empty;

        public int OnExecute(CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(Schema))
            {
                Console.WriteLine("A schema version is required.");
                return 1;
            }

            var indexStates = elasticClient.GetIndicesForVersion(elasticClient.AliasName, Schema);

            if (indexStates.Count == 0)
            {
                Console.WriteLine("No matching indices found.");
                return 1;
            }

            // TODO: should check if completed?
            var indexName = indexStates.OrderByDescending(x => x.Key).First().Key.Name;
            elasticClient.UpdateAlias(elasticClient.AliasName, indexName, Close);

            return 0;
        }
    }
}
