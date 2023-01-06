// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class UnrunnableProcessor : QueueProcessor<ScoreItem>
    {
        private static readonly string queue_name = $"score-index-{AppSettings.Schema}";

        internal UnrunnableProcessor()
            : base(new QueueConfiguration { InputQueueName = queue_name })
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();
        }

        protected override void ProcessResult(ScoreItem item)
        {
            throw new NotImplementedException();
        }
    }
}
