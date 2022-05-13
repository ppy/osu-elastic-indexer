// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class Processor : QueueProcessor<ScoreItem>
    {
        private static readonly string queueName = $"score-index-{AppSettings.Schema}";

        public string QueueName { get; private set; }

        private readonly BulkIndexingDispatcher<SoloScore> dispatcher;

        internal Processor(BulkIndexingDispatcher<SoloScore> dispatcher) : base(new QueueConfiguration { InputQueueName = queueName })
        {
            this.dispatcher = dispatcher;
            QueueName = queueName;
        }

        protected override void ProcessResult(ScoreItem item)
        {
            // TODO: set on a timer or something so it doesn't hit redis every run (also quits evenif queue is empty)
            if (!checkSchema())
                Environment.Exit(0); // TODO: use cancellation

            var add = new List<SoloScore>();
            var remove = new List<SoloScore>();
            var scores = item.Scores;

            foreach (var score in scores)
            {
                // TODO: have should index handled at reader?
                if (score.ShouldIndex)
                    add.Add(score);
                else
                    remove.Add(score);
            }

            dispatcher.Enqueue(add, remove);
        }

        private bool checkSchema()
        {
            var schema = Helpers.GetSchemaVersion();
            var matches = schema == AppSettings.Schema;
            if (!matches)
                Console.WriteLine($"Expecting schema {AppSettings.Schema}, got {schema}");

            return matches;
        }
    }
}
