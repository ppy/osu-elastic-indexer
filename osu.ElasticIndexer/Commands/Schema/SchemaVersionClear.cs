// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer.Commands.Schema
{
    [Command("clear", Description = "Clears the currently set active schema version.")]
    public class SchemaVersionClear
    {
        public int OnExecute(CancellationToken token)
        {
            Console.WriteLine(ConsoleColor.Yellow, "Unsetting schema...");
            RedisAccess.GetConnection().ClearCurrentSchema();
            return 0;
        }
    }
}
