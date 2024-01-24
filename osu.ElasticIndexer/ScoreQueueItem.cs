// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class ScoreQueueItem : QueueItem
    {
        public long ScoreId { get; init; }

        public override string ToString() => $"ScoreItem ScoreId: {ScoreId}";
    }
}
