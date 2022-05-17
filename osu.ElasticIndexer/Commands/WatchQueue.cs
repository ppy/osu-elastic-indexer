// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("queue", Description = "Watches queue and dispatches scores for indexing")]
    public class WatchQueue
    {
        public int OnExecute(CancellationToken token)
        {
            boot();
            new SoloScoreIndexer().Run(token);
            return 0;
        }

        private void boot()
        {
            var schema = new Redis().GetSchemaVersion();

            if (string.IsNullOrEmpty(schema))
                ConsoleColor.Yellow.WriteLine($"No existing schema version set, is this intended?");

            ConsoleColor.Green.WriteLine($"Running queue with schema version {AppSettings.Schema}");
        }
    }
}
