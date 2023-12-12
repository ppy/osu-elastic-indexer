// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using McMaster.Extensions.CommandLineUtils;
using osu.ElasticIndexer.Commands.Index;

namespace osu.ElasticIndexer.Commands
{
    [Command("index", Description = "Index management commands.")]
    [Subcommand(typeof(ListIndicesCommand))]
    [Subcommand(typeof(NukeAllIndicesCommand))]
    [Subcommand(typeof(CloseIndexCommand))]
    [Subcommand(typeof(DeleteIndexCommand))]
    [Subcommand(typeof(OpenIndexCommand))]
    [Subcommand(typeof(UpdateAliasCommand))]
    public class IndexCommands
    {
        public int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 1;
        }
    }
}
