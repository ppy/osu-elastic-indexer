// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;
using StackExchange.Redis;

namespace osu.ElasticIndexer.Commands.Index;

[Command("remove", Description = "Remove specified schema.")]
public class RemoveSchemaCommand
{
    private readonly ConnectionMultiplexer redis = RedisAccess.GetConnection();

    [Argument(0, "name", "The name of the schema to remove.")]
    public string Name { get; set; } = string.Empty;

    public int OnExecute(CancellationToken token)
    {
        redis.RemoveActiveSchema(Name);
        return 0;
    }
}
