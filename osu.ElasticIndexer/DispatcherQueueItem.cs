// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.ElasticIndexer
{
    public class DispatcherQueueItem<T> where T : HighScore
    {
        private static readonly List<T> EmptyList = new List<T>(0);

        public List<T> IndexItems { get; private set; }

        public List<T> DeleteItems { get; private set; }

        public DispatcherQueueItem(List<T> index, List<T> delete) {
            IndexItems = index ?? EmptyList;
            DeleteItems = delete ?? EmptyList;
        }
    }
}
