// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Dapper;
using McMaster.Extensions.CommandLineUtils;
using osu.ElasticIndexer.Commands;

namespace osu.ElasticIndexer
{
    [Command]
    [Subcommand(typeof(ClearQueueCommand))]
    [Subcommand(typeof(CloseIndexCommand))]
    [Subcommand(typeof(DeleteIndexCommand))]
    [Subcommand(typeof(ListIndicesCommand))]
    [Subcommand(typeof(IndexCommand))]
    [Subcommand(typeof(OpenIndexCommand))]
    [Subcommand(typeof(PumpAllScoresCommand))]
    [Subcommand(typeof(PumpFakeScoresCommand))]
    [Subcommand(typeof(PushFileCommand))]
    [Subcommand(typeof(SchemaVersionCommand))]
    [Subcommand(typeof(UpdateAliasCommand))]
    [Subcommand(typeof(WatchQueueCommand))]
    public class Program
    {
        public static void Main(string[] args)
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            CommandLineApplication.Execute<Program>(args);
        }

        public int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 1;
        }
    }
}
