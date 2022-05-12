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
        public int OnExecute(CancellationToken token)
        {
            var response = AppSettings.ELASTIC_CLIENT.Cat.Indices(x => x.Index($"{IndexHelper.INDEX_NAME}_*"));
            var closed = response.Records.Where(record => record.Status == "close");

            if (closed.Count() == 0)
            {
                Console.WriteLine("No indices to delete!");
                return 0;
            }

            var originalColour = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("The following indices will be deleted!");
            Console.ResetColor();
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
                AppSettings.ELASTIC_CLIENT.Indices.Delete(record.Index);
            }
            Console.WriteLine("done.");

            return 0;
        }
    }
}
