// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("clear", Description = "Clears the queue.")]
    public class ClearQueueCommand : ProcessorCommandBase
    {
        public int OnExecute(CancellationToken token)
        {
            Processor.ClearQueue();
            Console.WriteLine($"Queue {Processor.QueueName} cleared.");
            return 0;
        }
    }
}
