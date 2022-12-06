// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using McMaster.Extensions.CommandLineUtils;
using osu.ElasticIndexer.Commands.Queue;

namespace osu.ElasticIndexer.Commands
{
    [Command("queue", Description = "Queue management commands.")]
    [Subcommand(typeof(ClearQueueCommand))]
    [Subcommand(typeof(WatchQueueCommand))]
    [Subcommand(typeof(PumpAllScoresCommand))]
    [Subcommand(typeof(PumpFakeScoresCommand))]
    [Subcommand(typeof(PumpFileCommand))]
    [Subcommand(typeof(PumpScoreCommand))]
    public class QueueCommands
    {
        public int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 1;
        }
    }
}
