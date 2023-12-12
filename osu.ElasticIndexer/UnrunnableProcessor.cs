// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class UnrunnableProcessor : QueueProcessor<ScoreQueueItem>
    {
        internal UnrunnableProcessor()
            : base(new QueueConfiguration { InputQueueName = $"{AppSettings.AliasName}_{AppSettings.Schema}" })
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();
        }

        protected override void ProcessResult(ScoreQueueItem item)
        {
            throw new NotImplementedException();
        }
    }
}
