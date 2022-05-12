// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("all", Description = "Pumps scores through the queue for processing")]
    public class PumpAllScores
    {
        protected readonly Processor<SoloScore> Queue = new Processor<SoloScore>();

        [Option("--delay", Description = "Delay in milliseconds between reading chunks")]
        public int Delay { get; set; }

        public int OnExecute(CancellationToken cancellationToken)
        {
            var chunks = Model.Chunk<SoloScore>(AppSettings.ChunkSize);
            foreach (var scores in chunks)
            {
                var users = User.FetchUserMappings(scores);
                foreach (var score in scores)
                {
                    score.country_code = users[score.UserId].country_acronym;
                    Console.WriteLine($"Adding {score}");
                }

                Console.WriteLine($"Pushing {scores.Count} scores");
                Queue.PushToQueue(new ScoreItem(scores));

                if (Delay > 0)
                    Thread.Sleep(Delay);
            }

            return 0;
        }
    }
}
