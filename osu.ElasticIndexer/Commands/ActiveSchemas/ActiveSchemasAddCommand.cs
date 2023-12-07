// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer.Commands.ActiveSchemas
{
    [Command("add", Description = "Add a schema version to the list of versions being processed.")]
    public class ActiveSchemasAddCommand
    {
        [Argument(0, "schema", "The schema version to add as active.")]
        [Required]
        public string Schema { get; set; } = string.Empty;

        public int OnExecute(CancellationToken token)
        {
            var added = RedisAccess.GetConnection().AddActiveSchema(Schema);
            var text = added ? "Added" : "Already exists";

            Console.WriteLine(ConsoleColor.Green, $"{text}: {Schema}");

            return 0;
        }
    }
}
