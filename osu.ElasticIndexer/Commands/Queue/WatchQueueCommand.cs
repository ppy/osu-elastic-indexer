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
        public int OnExecute(CancellationToken token)
        {
            string currentIndex = RedisAccess.GetConnection().GetCurrentSchema();
            string proposedIndex = $"{new OsuElasticClient().AliasName}_{AppSettings.Schema}";

            if (string.IsNullOrEmpty(currentIndex))
                Console.WriteLine(ConsoleColor.Yellow, "WARNING: No current schema set, will set new schema as current");
            else if (currentIndex != proposedIndex)
                Console.WriteLine($"WARNING: Starting processing for schema version {proposedIndex} which is not current (current schema is {currentIndex})");

            Console.WriteLine(ConsoleColor.Green, $"Running queue with schema version {proposedIndex}");
            Thread.Sleep(5000);
            if (token.IsCancellationRequested)
                return -1;

            new SoloScoreIndexer().Run(token);

            return 0;
        }
    }
}
