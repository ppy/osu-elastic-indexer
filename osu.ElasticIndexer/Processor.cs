// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class Processor : QueueProcessor<ScoreItem>
    {
        private static readonly string queueName = $"score-index-{AppSettings.Schema}";

        public string QueueName { get; private set; }

        private readonly BulkIndexingDispatcher<SoloScore>? dispatcher;

        internal Processor() : base(new QueueConfiguration { InputQueueName = queueName })
        {
            QueueName = queueName;
        }

        internal Processor(BulkIndexingDispatcher<SoloScore> dispatcher) : this()
        {
            this.dispatcher = dispatcher;
        }

        protected override void ProcessResult(ScoreItem item)
        {
            // FIXME: error or have fake dispatcher when not needed.
            if (dispatcher == null)
                return;

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
    }
}
