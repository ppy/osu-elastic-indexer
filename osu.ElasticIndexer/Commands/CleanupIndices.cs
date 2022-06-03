// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("cleanup", Description = "Deletes closed indices")]
    public class CleanupIndices : ProcessorCommandBase
    {
        private readonly Client client = new Client();

        public int OnExecute(CancellationToken token)
        {
            var response = client.ElasticClient.Cat.Indices(x => x.Index($"{client.AliasName}_*"));
            var closed = response.Records.Where(record => record.Status == "close");

            if (!closed.Any())
            {
                Console.WriteLine("No indices to delete!");
                return 0;
            }

            ConsoleColor.Red.WriteLine("The following indices will be deleted!");

            foreach (var record in closed)
            {
                Console.WriteLine($"{record.Index}");
            }

            Console.WriteLine();

            if (!Prompt.GetYesNo("Delete these indices?", false, ConsoleColor.Yellow))
            {
                Console.WriteLine("aborted.");
                return 1;
            }

            Console.WriteLine();

            foreach (var record in closed)
            {
                Console.WriteLine($"deleting {record.Index}...");
                client.ElasticClient.Indices.Delete(record.Index);
            }

            Console.WriteLine("done.");

            return 0;
        }
    }
}
