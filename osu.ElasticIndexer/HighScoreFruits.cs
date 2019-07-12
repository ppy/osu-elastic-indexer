// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Dapper.Contrib.Extensions;

namespace osu.ElasticIndexer
{
    [RulesetId(2)]
    [Table("osu_scores_fruits_high")]
    public class HighScoreFruits : HighScore
    {
    }
}
