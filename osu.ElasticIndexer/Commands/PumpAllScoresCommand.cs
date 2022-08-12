// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("all", Description = "Pumps scores through the queue for processing")]
    public class PumpAllScoresCommand : ProcessorCommandBase
    {
        [Option("--delay", Description = "Delay in milliseconds between reading chunks")]
        public int Delay { get; set; }

        [Option("--from", Description = "Score id to resume from")]
        public long? From { get; set; }

        [Option("--switch", Description = "Update the configured schema in redis after completing")]
        public bool Switch { get; set; }

        [Option("--verbose", Description = "Fill your console with text")]
        public bool Verbose { get; set; }

        public int OnExecute(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();

            var redis = new Redis();
            var currentSchema = redis.GetSchemaVersion();
            Console.WriteLine(ConsoleColor.Cyan, $"Current schema version is: {currentSchema}");
            Console.WriteLine(ConsoleColor.Cyan, $"Pushing to queue with schema: {AppSettings.Schema}");

            if (Switch && currentSchema == AppSettings.Schema)
                Console.WriteLine(ConsoleColor.Yellow, "Queue watchers will not update the alias if schema does not change!");

            var startTime = DateTimeOffset.Now;
            Console.WriteLine(ConsoleColor.Cyan, $"Start read: {startTime}");

            var chunks = ElasticModel.Chunk<SoloScore>(AppSettings.BatchSize, From);

            foreach (var scores in chunks)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                List<ScoreItem> scoreItems = new List<ScoreItem>();

                foreach (var score in scores)
                {
                    score.country_code ??= "XX";
                    scoreItems.Add(new ScoreItem { Score = score });
                }

                if (Verbose)
                    Console.WriteLine($"Pushing {scoreItems.Count} scores");

                Processor.PushToQueue(scoreItems);

                if (!Verbose)
                    Console.WriteLine($"Pushed {scores.LastOrDefault()}");

                if (Delay > 0)
                    Thread.Sleep(Delay);
            }

            var endTime = DateTimeOffset.Now;
            Console.WriteLine(ConsoleColor.Cyan, $"End read: {endTime}, time taken: {endTime - startTime}");

            if (Switch)
                redis.SetSchemaVersion(AppSettings.Schema);

            return 0;
        }
    }
}
