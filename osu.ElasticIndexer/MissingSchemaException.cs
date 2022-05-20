// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    public class MissingSchemaException : Exception
    {
        public MissingSchemaException() : base("A schema version is required.") { }
    }
}
