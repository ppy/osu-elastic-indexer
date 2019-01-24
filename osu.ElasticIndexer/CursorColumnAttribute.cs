// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
