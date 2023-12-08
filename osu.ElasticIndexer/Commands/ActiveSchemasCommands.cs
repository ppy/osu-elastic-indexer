// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using McMaster.Extensions.CommandLineUtils;
using osu.ElasticIndexer.Commands.ActiveSchemas;

namespace osu.ElasticIndexer.Commands
{
    [Command("active-schemas", Description = "Active (queue being processed) schema management commands.")]
    [Subcommand(typeof(ActiveSchemasAddCommand))]
    [Subcommand(typeof(ActiveSchemasRemoveCommand))]
    public class ActiveSchemasCommands
    {
        public int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 1;
        }
    }
}
