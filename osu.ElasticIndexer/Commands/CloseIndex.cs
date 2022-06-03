// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("close", Description = "Closes unused indices")]
    public class CloseIndex : ProcessorCommandBase
    {
        [Argument(0, "name", "The index to close. All unused indices are closed if not specified.")]
        public string? Name { get; set; }

        private readonly Client client = new Client();

        public int OnExecute(CancellationToken token)
        {
            var indices = string.IsNullOrEmpty(Name) ? client.GetIndices(client.AliasName) : client.GetIndex(Name);
            var unaliasedIndices = indices.Where(entry => entry.Value.Aliases.Count == 0);

            if (!unaliasedIndices.Any())
            {
                Console.WriteLine("No indices to close!");
                return 0;
            }

            Console.WriteLine("The following indices will be closed:");
            foreach (var entry in unaliasedIndices)
            {
                Console.WriteLine(entry.Key.Name);
            }

            Console.WriteLine();
            if (!Prompt.GetYesNo("Close these indices?", false, ConsoleColor.Yellow))
            {
                Console.WriteLine("aborted.");
                return 1;
            }

            Console.WriteLine();
            foreach (var entry in unaliasedIndices)
            {
                Console.WriteLine($"closing {entry.Key.Name}...");
                client.ElasticClient.Indices.Close(entry.Key.Name);
            }
            Console.WriteLine("done.");

            return 0;
        }
    }
}
