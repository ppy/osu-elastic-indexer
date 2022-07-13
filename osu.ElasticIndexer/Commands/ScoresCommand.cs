// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("scores", Description = "Namespace for score indexing commands")]
    [Subcommand(typeof(ScoresDeleteCommand))]
    [Subcommand(typeof(ScoresIndexCommand))]
    public class ScoresCommand : ProcessorCommandBase
    {
        public int OnExecute(CancellationToken cancellationToken)
        {
            return 0;
        }
    }
}
