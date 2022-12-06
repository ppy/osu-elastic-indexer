// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("pump-score", Description = "Pump a single score through the queue for indexing by id.")]
    public class PumpScoreCommand
    {
        [Argument(1)]
        [Required]
        public string ScoreId { get; set; } = string.Empty;

        public int OnExecute(CancellationToken cancellationToken)
        {
            var processor = new UnrunnableProcessor();

            var id = long.Parse(ScoreId);
            var scoreItem = new ScoreItem { ScoreId = id };
            processor.PushToQueue(scoreItem);

            Console.WriteLine(ConsoleColor.Green, $"Queued to {processor.QueueName}: {scoreItem}");

            return 0;
        }
    }
}
