// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("scores", Description = "Namespace for score indexing commands")]
    public class ScoresCommand : ProcessorCommandBase
    {
        [Argument(0)]
        [Required]
        [AllowedValues("delete", "index", IgnoreCase = true)]
        public string Action { get; set; } = string.Empty;

        [Argument(1)]
        [Required]
        public string ScoreId { get; set; } = string.Empty;

        public int OnExecute(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();

            var Id = long.Parse(ScoreId);
            var scoreItem = new ScoreItem() { Action = Action, ScoreId = Id };
            Processor.PushToQueue(scoreItem);

            Console.WriteLine(ConsoleColor.Cyan, $"Queued to {Processor.QueueName}: {scoreItem}");

            return 0;
        }
    }
}
