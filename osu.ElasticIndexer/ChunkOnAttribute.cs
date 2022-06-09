// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    /// <summary>
    /// Attributes which column to use as the cursor column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ChunkOnAttribute : Attribute
    {
        public string? CursorColumn { get; set; }

        public string? Max { get; set; }

        public string? Query { get; set; }
    }
}
