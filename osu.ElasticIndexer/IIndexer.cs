// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    internal interface IIndexer
    {
        event EventHandler<IndexCompletedArgs> IndexCompleted;

        /// <summary>
        /// Does the indexer run as a crawler or not.
        /// </summary>
        bool IsCrawler { get; set; }

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
