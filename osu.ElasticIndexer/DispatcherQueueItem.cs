// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.ElasticIndexer
{
    public class DispatcherQueueItem<T> where T : HighScore
    {
        private static readonly List<string> EmptyDeleteList = new List<string>(0);

        public List<T> IndexItems { get; private set; }

        public List<string> DeleteIds { get; private set; }

        public DispatcherQueueItem(List<T> index, List<string> delete = null) {
            IndexItems = index.Where(x => x.ShouldIndex).ToList(); // this should probably not go here?
            DeleteIds = delete ?? EmptyDeleteList;
        }
    }
}
