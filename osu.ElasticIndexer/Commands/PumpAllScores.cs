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

        public int OnExecute(CancellationToken cancellationToken)
        {
            var chunks = Model.Chunk<SoloScore>(AppSettings.BatchSize);
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

            return 0;
        }
    }
}
