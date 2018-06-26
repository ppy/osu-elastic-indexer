// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;

namespace osu.ElasticIndexer
{
    public class IndexCompletedArgs
    {
        public string Alias;
        public DateTime CompletedAt;
        public long Count;
        public string Index;
        public DateTime StartedAt;

        public TimeSpan TimeTaken => CompletedAt - StartedAt;
    }
}
