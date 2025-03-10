// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;

namespace osu.ElasticIndexer.Commands.Index
{
    [Command("delete", Description = "Deletes closed indices.")]
    public class DeleteIndexCommand
    {
        [Argument(0, "name", "The index to delete. All closed indices are deleted if not specified.")]
        public string? Name { get; set; }

        private readonly ConnectionMultiplexer redis = RedisAccess.GetConnection();
        private readonly OsuElasticClient elasticClient = new OsuElasticClient();

        public int OnExecute(CancellationToken token)
        {
            string? index = string.IsNullOrEmpty(Name) ? $"{AppSettings.AliasName}_*" : Name;
            var response = elasticClient.Cat.Indices(x => x.Index(index));
            var closed = response.Records.Where(record => record.Status == "close");

            if (!closed.Any())
            {
                Console.WriteLine("No indices to delete!");
                return 0;
            }

            Console.WriteLine(ConsoleColor.Red, "The following indices will be deleted!");

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
                elasticClient.Indices.Delete(record.Index);
            }

            Console.WriteLine("done.");

            return ListIndicesCommand.ListSchemas(redis, elasticClient);
        }
    }
}
