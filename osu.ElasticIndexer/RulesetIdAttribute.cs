// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    /// <summary>
    /// Integer value of the game mode.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RulesetId : Attribute
    {
        public int Id { get; }

        public RulesetId(int id) => Id = id;
    }
}
