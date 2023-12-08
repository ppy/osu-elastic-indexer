// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("watch", Description = "Watches queue and dispatches scores for indexing.")]
    public class WatchQueueCommand
    {
        [Option("--set-current", Description = "Whether to immediately set the schema being watched as the current schema. This is equivalent to running `schema set SCHEMA`. Generally used for test / debug setups.")]
        public bool SetCurrent { get; set; }

        public int OnExecute(CancellationToken token)
        {
            var schema = RedisAccess.GetConnection().GetCurrentSchema();

            if (string.IsNullOrEmpty(schema))
            {
                Console.WriteLine(ConsoleColor.Yellow, "No existing schema version set, is this intended?");
                Thread.Sleep(5000);
                if (token.IsCancellationRequested)
                    return -1;
            }

            if (schema != AppSettings.Schema)
            {
                Console.WriteLine($"WARNING: Starting processing for schema version {AppSettings.Schema} which is not current (current schema is {schema})");
                Thread.Sleep(5000);
                if (token.IsCancellationRequested)
                    return -1;
            }

            Console.WriteLine(ConsoleColor.Green, $"Running queue with schema version {AppSettings.Schema}");
            Thread.Sleep(5000);
            if (token.IsCancellationRequested)
                return -1;

            new SoloScoreIndexer().Run(token, SetCurrent);

            return 0;
        }
    }
}
