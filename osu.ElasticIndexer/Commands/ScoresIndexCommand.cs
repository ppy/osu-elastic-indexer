// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("index", Description = "Queues a score for indexing")]
    public class ScoresIndexCommand : ProcessorCommandBase
    {
        [Argument(0)]
        [Required]
        public long ScoreId { get; set; }

        public int OnExecute(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();

            var redis = new Redis();
            var currentSchema = redis.GetSchemaVersion();
            Console.WriteLine(ConsoleColor.Cyan, $"Current schema version is: {currentSchema}");
            Console.WriteLine(ConsoleColor.Cyan, $"Pushing to queue with schema: {AppSettings.Schema}");

            var scoreItem = new ScoreItem(new SoloScore() { id = ScoreId }) { Action = "index" };
            Processor.PushToQueue(scoreItem);

            Console.WriteLine(ConsoleColor.Cyan, $"Queued index: {scoreItem}");

            return 0;
        }
    }
}
