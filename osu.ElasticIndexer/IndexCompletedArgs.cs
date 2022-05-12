// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    public class IndexCompletedArgs
    {
        public string? Alias { get; set; }
        public DateTime CompletedAt { get; set; }
        public long Count { get; set; }
        public string? Index { get; set; }
        public DateTime StartedAt { get; set; }

        public TimeSpan TimeTaken => CompletedAt - StartedAt;
    }
}
