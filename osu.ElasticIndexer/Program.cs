// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Dapper;
using McMaster.Extensions.CommandLineUtils;
using osu.ElasticIndexer.Commands;
using StatsdClient;

namespace osu.ElasticIndexer
{
    [Command]
    [Subcommand(typeof(CleanupIndices))]
    [Subcommand(typeof(ClearQueue))]
    [Subcommand(typeof(CloseIndices))]
    [Subcommand(typeof(PumpAllScores))]
    [Subcommand(typeof(PumpFakeScores))]
    [Subcommand(typeof(SchemaVersion))]
    [Subcommand(typeof(UpdateAlias))]
    [Subcommand(typeof(WatchQueue))]
    public class Program
    {
        public static void Main(string[] args)
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            DogStatsd.Configure(new StatsdConfig
            {
                StatsdServerName = "127.0.0.1",
                Prefix = "elasticsearch.scores"
            });

            CommandLineApplication.Execute<Program>(args);
        }

        public int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 1;
        }
    }
}
