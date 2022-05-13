// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class Processor : QueueProcessor<ScoreItem>
    {
        private static readonly string queueName = $"score-index-{AppSettings.Schema}";

        public string QueueName { get; private set; }

        private readonly BulkIndexingDispatcher<SoloScore> dispatcher;

        internal Processor(BulkIndexingDispatcher<SoloScore> dispatcher) : base(new QueueConfiguration { InputQueueName = queueName, BatchSize = AppSettings.ChunkSize })
        {
            this.dispatcher = dispatcher;
            QueueName = queueName;
        }

        protected override void ProcessResults(IEnumerable<ScoreItem> items)
        {
            var add = new List<SoloScore>();
            var remove = new List<SoloScore>();

            foreach (var item in items)
            {
                // TODO: have should index handled at reader?
                if (item.Score.ShouldIndex)
                    add.Add(item.Score);
                else
                    remove.Add(item.Score);
            }

            dispatcher.Enqueue(add, remove);
        }
    }
}
