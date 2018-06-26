// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;

namespace osu.ElasticIndexer
{
    public class IndexCompletedArgs
    {
        public string Alias { get; set; }
        public DateTime CompletedAt { get; set; }
        public long Count { get; set; }
        public string Index { get; set; }
        public DateTime StartedAt { get; set; }

        public TimeSpan TimeTaken => CompletedAt - StartedAt;
    }
}
