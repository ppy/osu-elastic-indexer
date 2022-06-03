// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("open", Description = "Opens an index")]
    public class OpenIndex : ProcessorCommandBase
    {
        [Argument(0, "name", "The index to open.")]
        [Required]
        public string Name { get; } = string.Empty;

        private readonly Client client = new Client();

        public int OnExecute()
        {
            var response = client.ElasticClient.Indices.Open(Name);
            Console.WriteLine(response);
            return 0;
        }
    }
}
