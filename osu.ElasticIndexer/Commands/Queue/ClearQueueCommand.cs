// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("clear", Description = "Clears the queue.")]
    public class ClearQueueCommand
    {
        public int OnExecute(CancellationToken token)
        {
            var processor = new UnrunnableProcessor();

            processor.ClearQueue();
            Console.WriteLine($"Queue {processor.QueueName} cleared.");
            return 0;
        }
    }
}
