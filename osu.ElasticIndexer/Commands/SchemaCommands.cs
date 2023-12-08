// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using McMaster.Extensions.CommandLineUtils;
using osu.ElasticIndexer.Commands.Schema;

namespace osu.ElasticIndexer.Commands
{
    [Command("schema", Description = "Current schema version management commands.")]
    [Subcommand(typeof(SchemaVersionClear))]
    [Subcommand(typeof(SchemaVersionSet))]
    public class SchemaCommands
    {
        public int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 1;
        }
    }
}
