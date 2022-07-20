// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.ElasticIndexer
{
    public class ProcessableItemsBuffer
    {
        public readonly HashSet<long> LookupIds = new HashSet<long>();
        public readonly List<SoloScore> Add = new List<SoloScore>();
        public readonly List<string> Remove = new List<string>();
    }
}
