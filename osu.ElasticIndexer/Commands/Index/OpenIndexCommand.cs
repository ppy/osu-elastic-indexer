// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("open", Description = "Opens an index.")]
    public class OpenIndexCommand
    {
        [Argument(0, "name", "The index to open.")]
        [Required]
        public string Name { get; } = string.Empty;

        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        public int OnExecute()
        {
            var response = elasticClient.Indices.Open(Name);
            Console.WriteLine(response.ToString());
            return 0;
        }
    }
}
