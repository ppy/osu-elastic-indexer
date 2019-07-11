// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.ElasticIndexer
{
    public class DispatcherQueueItem<T> where T : HighScore
    {
        public List<T> IndexItems { get; private set; }
        public List<T> DeleteItems { get; private set; }

        public DispatcherQueueItem(List<T> index, List<T> delete = null) {
            IndexItems = index;
            DeleteItems = delete;
        }
    }
}
