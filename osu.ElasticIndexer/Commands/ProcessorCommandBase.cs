// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.ElasticIndexer.Commands
{
    public abstract class ProcessorCommandBase
    {
        protected readonly Processor<SoloScore> Processor = new Processor<SoloScore>();
    }
}
