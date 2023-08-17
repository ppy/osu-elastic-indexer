// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("pump-all", Description = "Pumps scores through the queue for processing.")]
    public class PumpAllScoresCommand
    {
        [Option("--delay", Description = "Delay in milliseconds between reading chunks.")]
        public int Delay { get; set; }

        [Option("--from", Description = "Score id to resume from.")]
        public long? From { get; set; }

        [Option("--switch", Description = "Update the configured schema in redis after completing.")]
        public bool Switch { get; set; }

        private CancellationToken cancellationToken;

        private UnrunnableProcessor processor = null!;

        public int OnExecute(CancellationToken cancellationToken)
        {
            processor = new UnrunnableProcessor();

            this.cancellationToken = cancellationToken;

            var redis = new Redis();
            var currentSchema = redis.GetSchemaVersion();
            Console.WriteLine(ConsoleColor.Green, $"Current schema version is: {currentSchema}");
            Console.WriteLine(ConsoleColor.Green, $"Pushing to queue with schema: {AppSettings.Schema}");

            if (Switch && currentSchema == AppSettings.Schema)
                Console.WriteLine(ConsoleColor.Yellow, "Queue watchers will not update the alias if schema does not change!");

            var startTime = DateTimeOffset.Now;
            Console.WriteLine(ConsoleColor.Cyan, $"Start read: {startTime}");

            var lastId = queueScores(From);

            var endTime = DateTimeOffset.Now;
            Console.WriteLine(ConsoleColor.Cyan, $"End read: {endTime}, time taken: {endTime - startTime}");

            if (Switch)
            {
                redis.SetCurrentSchema(AppSettings.Schema);
                Console.WriteLine(ConsoleColor.Yellow, $"Schema version set to {AppSettings.Schema}, queueing scores > {lastId}");
                queueScores(lastId);

                var switchEndTime = DateTimeOffset.Now;
                Console.WriteLine(ConsoleColor.Cyan, $"End read after switch: {switchEndTime}, time taken: {switchEndTime - startTime}");
            }

            return 0;
        }

        private long? queueScores(long? from)
        {
            using (var mySqlConnection = processor.GetDatabaseConnection())

            {
                var chunks = ElasticModel.Chunk<SoloScore>(mySqlConnection, "preserve = 1", AppSettings.BatchSize, from);
                SoloScore? last = null;

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

                    Console.WriteLine($"Pushing {scoreItems.Count} scores");

                    while (processor.GetQueueSize() > 1000000)
                    {
                        Console.WriteLine($"Paused due to excessive queue length ({processor.GetQueueSize()})");
                        Thread.Sleep(30000);
                    }

                    processor.PushToQueue(scoreItems);

                    Console.WriteLine($"Pushed {scores.LastOrDefault()}");
                    last = scores.LastOrDefault();

                    if (Delay > 0)
                        Thread.Sleep(Delay);
                }

                return last?.id;
            }
        }
    }
}
