// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.ElasticIndexer
{
    public class DispatcherQueueItem<T> where T : Model
    {
        public IEnumerable<T> ItemsToDelete { get; private set; }
        public IEnumerable<T> ItemsToIndex { get; private set; }

        public DispatcherQueueItem(IEnumerable<T>? add, IEnumerable<T>? remove) {
            ItemsToIndex = add ?? Enumerable.Empty<T>();
            ItemsToDelete = remove ?? Enumerable.Empty<T>();
        }
    }
}
