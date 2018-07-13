// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;

namespace osu.ElasticIndexer
{
    internal interface IIndexer
    {
        event EventHandler<IndexCompletedArgs> IndexCompleted;

        /// <summary>
        /// The index's name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// The ID from which to resume indexing from. If null, the most recent ID is used.
        /// </summary>
        long? ResumeFrom { get; set; }

        /// <summary>
        /// The index suffix (generally a timestamp string).
        /// </summary>
        string Suffix { get; set; }

        void Run();
    }
}
