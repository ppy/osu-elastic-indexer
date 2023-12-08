// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using Nest;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer.Commands.Index;

[Command("nuke", Description = "Resets everything with fire.")]
public class NukeAllIndicesCommand : ListIndicesCommand
{
    public override int OnExecute(CancellationToken token)
    {
        // Don't exit on error as we're likely trying to fix an error.
        base.OnExecute(token);

        Console.WriteLine();
        Console.WriteLine("PREPARING TO NUKE ALL ELASTICSEARCH INDICES WITH FIRE!");
        Console.WriteLine();

        Thread.Sleep(10000);
        if (token.IsCancellationRequested)
            return -1;

        System.Console.WriteLine("Unsetting current schema..");
        Redis.ClearCurrentSchema();

        System.Console.WriteLine("Removing all active schemas..");
        foreach (string schema in Redis.GetActiveSchemas())
            Redis.RemoveActiveSchema(schema);

        System.Console.WriteLine("Removing all indices..");

        var records = ElasticClient.Cat.Indices(x => x.Index(Indices.All)).Records;

        foreach (var record in records)
        {
            System.Console.WriteLine($"Deleting {record.Index}..");
            ElasticClient.Indices.Delete(record.Index);
        }

        return 0;
    }
}
