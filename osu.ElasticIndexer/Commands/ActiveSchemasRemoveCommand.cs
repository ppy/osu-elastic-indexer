// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("remove", Description = "Removes a schema version from the list of versions being processed.")]
    public class ActiveSchemasRemoveCommand
    {
        [Argument(0)]
        [Required]
        public string Schema { get; set; } = string.Empty;

        public int OnExecute(CancellationToken token)
        {
            var exists = new Redis().RemoveActiveSchema(Schema);
            var text = exists ? "Removed" : "Did not exist";

            Console.WriteLine(ConsoleColor.Green, $"{text}: {Schema}");

            return 0;
        }
    }
}
