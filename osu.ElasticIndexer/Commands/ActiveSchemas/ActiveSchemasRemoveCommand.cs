// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer.Commands.ActiveSchemas
{
    [Command("remove", Description = "Removes a schema version from the list of versions being processed.")]
    public class ActiveSchemasRemoveCommand
    {
        [Argument(0, "schema", "The schema version to remove from active.")]
        [Required]
        public string Schema { get; set; } = string.Empty;

        public int OnExecute(CancellationToken token)
        {
            var exists = RedisAccess.GetConnection().RemoveActiveSchema(Schema);
            var text = exists ? "Removed" : "Did not exist";

            Console.WriteLine(ConsoleColor.Green, $"{text}: {Schema}");

            return 0;
        }
    }
}
