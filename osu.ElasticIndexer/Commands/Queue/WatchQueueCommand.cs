// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("watch", Description = "Watches queue and dispatches scores for indexing.")]
    public class WatchQueueCommand
    {
        public int OnExecute(CancellationToken token)
        {
            var schema = new Redis().GetSchemaVersion();

            if (string.IsNullOrEmpty(schema))
                Console.WriteLine(ConsoleColor.Yellow, "No existing schema version set, is this intended?");

            if (schema != AppSettings.Schema)
                Console.WriteLine($"WARNING: Starting processing for schema version {AppSettings.Schema} which is not current (current schema is {schema})");

            Console.WriteLine(ConsoleColor.Green, $"Running queue with schema version {AppSettings.Schema}");

            new SoloScoreIndexer().Run(token);

            return 0;
        }
    }
}
