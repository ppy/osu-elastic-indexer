// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class ScoreItem : QueueItem
    {
        public long? ScoreId { get; init; }

        // ScoreId is always preferred if present (this property is ignored).
        // Note that this is generally not used anymore. Consider removing in the future unless a use case comes up?
        public SoloScore? Score { get; init; }

        public override string ToString() => Score != null ? $"ScoreItem Score: {Score.id}" : $"ScoreItem ScoreId: {ScoreId}";
    }
}
