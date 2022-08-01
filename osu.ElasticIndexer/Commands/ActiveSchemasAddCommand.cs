// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("add", Description = "Add a schema version to the list of versions being processed.")]
    public class ActiveSchemasAddCommand
    {
        [Argument(0)]
        [Required]
        public string Schema { get; set; } = string.Empty;

        public int OnExecute(CancellationToken token)
        {
            var added = new Redis().AddActiveSchema(Schema);

            if (added)
                Console.WriteLine(ConsoleColor.Green, $"Added: {Schema}");
            else
                Console.WriteLine(ConsoleColor.Green, $"Already exists: {Schema}");

            return 0;
        }
    }
}
