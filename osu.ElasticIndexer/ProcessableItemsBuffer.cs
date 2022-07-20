// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.ElasticIndexer
{
    public class ProcessableItemsBuffer
    {
        /// <summary>
        /// A set of all score IDs which have arrived but are not yet determined to be an addition or deletion.
        /// These should be processed into either <see cref="Additions"/> or <see cref="Deletions"/>.
        /// </summary>
        public readonly HashSet<long> ScoreIdsForLookup = new HashSet<long>();

        /// <summary>
        /// New scores which should be indexed.
        /// </summary>
        public readonly List<SoloScore> Additions = new List<SoloScore>();

        /// <summary>
        /// Score IDs which should be purged from the index is they are present.
        /// </summary>
        public readonly List<long> Deletions = new List<long>();
    }
}
