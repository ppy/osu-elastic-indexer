// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("pump-score", Description = "Pump a single score through the queue for indexing by id.")]
    public class PumpScoreCommand : ProcessorCommandBase
    {
        [Argument(1)]
        [Required]
        public string ScoreId { get; set; } = string.Empty;

        public int OnExecute(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();

            var id = long.Parse(ScoreId);
            var scoreItem = new ScoreItem { ScoreId = id };
            Processor.PushToQueue(scoreItem);

            Console.WriteLine(ConsoleColor.Green, $"Queued to {Processor.QueueName}: {scoreItem}");

            return 0;
        }
    }
}
