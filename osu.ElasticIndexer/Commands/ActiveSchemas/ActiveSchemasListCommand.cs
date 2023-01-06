// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.ActiveSchemas
{
    [Command("list", Description = "Lists known schema versions being processed.")]
    public class ActiveSchemasListCommand
    {
        public int OnExecute(CommandLineApplication app)
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
