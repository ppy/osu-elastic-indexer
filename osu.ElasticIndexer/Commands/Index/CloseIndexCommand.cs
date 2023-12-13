// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("close", Description = "Closes unused indices.")]
    public class CloseIndexCommand : ListIndicesCommand
    {
        [Argument(0, "name", "The index to close. All unused indices are closed if not specified.")]
        public string? Name { get; set; }

        public override int OnExecute(CancellationToken token)
        {
            if (base.OnExecute(token) != 0)
                return -1;

            var indices = string.IsNullOrEmpty(Name) ? ElasticClient.GetIndices($"{AppSettings.AliasName}_*") : ElasticClient.GetIndex(Name);

            var closeableIndices = indices.Where(entry => entry.Value.Aliases.Count == 0);

            if (!closeableIndices.Any())
            {
                Console.WriteLine(ConsoleColor.Yellow, "No indices to close!");
                return 0;
            }

            Console.WriteLine("The following indices will be closed:");

            foreach (var entry in closeableIndices)
                Console.WriteLine(entry.Key.Name);

            Console.WriteLine();

            if (!Prompt.GetYesNo("Close these indices?", false, ConsoleColor.Yellow))
            {
                Console.WriteLine(ConsoleColor.Red, "aborted.");
                return 1;
            }

            Console.WriteLine();

            foreach (var entry in closeableIndices)
            {
                if (entry.Key.Name == Redis.GetCurrentSchema())
                {
                    Console.WriteLine(ConsoleColor.Red, $"Index {entry.Key.Name} is set as current schema. Cannot close.");
                    return -1;
                }

                System.Console.WriteLine($"Removing {entry.Key.Name} from active schemas..");
                Redis.RemoveActiveSchema(entry.Key.Name);

                Console.WriteLine($"Closing {entry.Key.Name}...");
                var response = ElasticClient.Indices.Close(entry.Key.Name);

                if (!response.IsValid)
                {
                    Console.WriteLine($"Error: {response.ServerError}");
                    return -1;
                }
            }

            Console.WriteLine("done.");

            return 0;
        }
    }
}
