// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("index", Description = "Queue a score for indexing by id.")]
    public class IndexCommand : ProcessorCommandBase
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

            Console.WriteLine(ConsoleColor.Cyan, $"Queued to {Processor.QueueName}: {scoreItem}");

            return 0;
        }
    }
}
