// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.ElasticIndexer.Commands.ActiveSchemas;

namespace osu.ElasticIndexer.Commands
{
    [Command("active-schemas", Description = "Lists known schema versions being processed.")]
    [Subcommand(typeof(ActiveSchemasAddCommand))]
    [Subcommand(typeof(ActiveSchemasRemoveCommand))]
    public class ActiveSchemasCommands
    {
        public int OnExecute(CancellationToken token)
        {
            var value = new Redis().GetActiveSchemas();

            Console.WriteLine(
                value.Length == 0
                    ? "No known schema versions currently being processed."
                    : $"Schema versions currently being processed: {string.Join(", ", value)}"
            );

            return 0;
        }
    }
}
