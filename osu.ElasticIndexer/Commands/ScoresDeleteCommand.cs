// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("delete", Description = "Queues a score for deletion")]
    public class ScoresDeleteCommand : ProcessorCommandBase
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

            var scoreItem = new ScoreItem(new SoloScore() { id = ScoreId }, "delete");
            Processor.PushToQueue(scoreItem);

            Console.WriteLine(ConsoleColor.Cyan, $"Queued delete: {scoreItem}");

            return 0;
        }
    }
}
