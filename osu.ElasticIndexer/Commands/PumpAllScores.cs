// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("all", Description = "Pumps scores through the queue for processing")]
    public class PumpAllScores : ProcessorCommandBase
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
            var redis = new Redis();
            var currentSchema = redis.GetSchemaVersion();
            ConsoleColor.Cyan.WriteLine($"Current schema version is: {currentSchema}");
            ConsoleColor.Cyan.WriteLine($"Pushing to queue with schema: {AppSettings.Schema}");

            if (Switch && currentSchema == AppSettings.Schema)
                ConsoleColor.Yellow.WriteLine("Queue watchers will not update the alias if schema does not change!");

            var startTime = DateTimeOffset.Now;
            ConsoleColor.Cyan.WriteLine($"Start read: {startTime}");

            var chunks = Model.Chunk<SoloScore>(AppSettings.BatchSize, From);
            foreach (var scores in chunks)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                foreach (var score in scores)
                {
                    if (score.country_code == null)
                        score.country_code = "XX";

                    if (Verbose)
                        Console.WriteLine($"Pushing {score}");

                    Processor.PushToQueue(new ScoreItem(score));
                }

                if (!Verbose)
                    Console.WriteLine($"Pushed {scores.LastOrDefault()}");

                if (Delay > 0)
                    Thread.Sleep(Delay);
            }

            var endTime = DateTimeOffset.Now;
            ConsoleColor.Cyan.WriteLine($"End read: {endTime}, time taken: {endTime - startTime}");

            if (Switch)
                redis.SetSchemaVersion(AppSettings.Schema);

            return 0;
        }
    }
}
