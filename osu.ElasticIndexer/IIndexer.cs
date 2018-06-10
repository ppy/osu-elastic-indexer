// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;

namespace osu.ElasticIndexer
{
    interface IIndexer
    {
        string Name { get; set; }
        long? ResumeFrom { get; set; }
        string Suffix { get; set; }

        void Run();
    }
}
