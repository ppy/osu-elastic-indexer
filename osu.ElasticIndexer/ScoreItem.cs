// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class ScoreItem : QueueItem
    {
        public List<SoloScore> Scores { get; private set; }

        public ScoreItem(List<SoloScore> scores)
        {
            Scores = scores;
        }
    }
}
