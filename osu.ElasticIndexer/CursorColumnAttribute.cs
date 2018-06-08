// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;

namespace osu.ElasticIndexer
{
    /// <summary>
    /// Attributes which column to use as the cursor column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CursorColumnAttribute : Attribute
    {
        public string Name { get; }

        public CursorColumnAttribute(string name) => Name = name;
    }
}
