// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        public long? From { get; }

        [Option("--switch", Description = "Update the configured schema in redis after completing")]
        public bool Switch { get; }

        public int OnExecute(CancellationToken cancellationToken)
        {
            var redis = new Redis();
            var currentSchema = redis.GetSchemaVersion();
            ConsoleColor.Cyan.WriteLine($"Current schema version is: {currentSchema}");
            ConsoleColor.Cyan.WriteLine($"Pushing to queue with schema: {AppSettings.Schema}");

            if (Switch && currentSchema == AppSettings.Schema)
                ConsoleColor.Yellow.WriteLine("Queue watchers will not update the alias if schema does not change!");

            var chunks = Model.Chunk<SoloScore>(AppSettings.BatchSize, From);
            foreach (var scores in chunks)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var users = User.FetchUserMappings(scores);
                foreach (var score in scores)
                {
                    score.country_code = users.ContainsKey(score.user_id) ? users[score.user_id].country_acronym : "XX";
                    Console.WriteLine($"Pushing {score}");
                    Processor.PushToQueue(new ScoreItem(score));
                }

                if (Delay > 0)
                    Thread.Sleep(Delay);
            }

            if (Switch)
                redis.SetSchemaVersion(AppSettings.Schema);

            return 0;
        }
    }
}
