// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.Serialization;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class ScoreItem : QueueItem
    {
        // TODO :figure out what's the deal with constructor not working?
        public string? Action { get; set; }
        public long? ScoreId { get; set; }

        [IgnoreDataMember]
        public string? ParsedAction => Action?.ToLowerInvariant();

        public SoloScore? Score { get; set; }

        public ScoreItem()
        {
        }

        public ScoreItem(SoloScore score)
        {
            Score = score;
        }

        public override string ToString() => Score != null ? $"ScoreItem Score: {Score.id}" : $"ScoreItem ScoreId: {ScoreId} {Action} ";
    }
}
