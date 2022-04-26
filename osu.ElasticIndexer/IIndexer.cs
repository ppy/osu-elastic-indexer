// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        void Run();
    }
}
