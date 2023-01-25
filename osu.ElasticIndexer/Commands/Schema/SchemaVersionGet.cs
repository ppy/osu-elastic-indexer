// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Schema
{
    [Command("get", Description = "Get the currently set index schema version.")]
    public class SchemaVersionGet
    {
        public int OnExecute(CancellationToken token)
        {
            var value = new Redis().GetSchemaVersion();
            Console.WriteLine($"Current schema version is {value}");
            return 0;
        }
    }
}
