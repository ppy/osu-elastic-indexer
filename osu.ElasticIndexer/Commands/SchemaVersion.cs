// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("schema", Description = "Gets or sets the current index schema version to use")]
    [Subcommand(typeof(SchemaVersionClear))]
    public class SchemaVersion
    {
        [Option("--schema", Description = "The schema version")]
        public string? Schema { get; set; }

        public int OnExecute(CancellationToken token)
        {
            var redis = new Redis();

            if (Schema == null)
            {
                var value = redis.GetSchemaVersion();
                Console.WriteLine($"Current schema version is {value}");
            }
            else
            {
                redis.SetSchemaVersion(Schema);
                Console.WriteLine($"Schema version set to {Schema}");
            }

            return 0;
        }
    }
}
