// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.Contrib.Extensions;
using Nest;
using Newtonsoft.Json;

namespace osu.ElasticIndexer
{
    [CursorColumn("score_id")]
    [Table("solo_scores_performance")]
    public class SoloScorePerformance : Model
    {
        [Computed]
        public override ulong CursorValue => score_id;

        public double? pp { get; set; }

        public ulong score_id { get; set; }

        public override string ToString() => $"score_id: {score_id} pp: {pp}";
    }
}
