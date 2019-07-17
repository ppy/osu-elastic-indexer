// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace osu.ElasticIndexer
{
    public class DispatcherQueueItem<T> where T : HighScore
    {
        private static readonly IEnumerable<T> empty_list = new ReadOnlyCollection<T>(new List<T>(0));

        public IEnumerable<T> ItemsToDelete { get; private set; }
        public IEnumerable<T> ItemsToIndex { get; private set; }

        public DispatcherQueueItem(IEnumerable<T> add, IEnumerable<T> remove) {
            ItemsToIndex = add ?? empty_list;
            ItemsToDelete = remove ?? empty_list;
        }
    }
}
