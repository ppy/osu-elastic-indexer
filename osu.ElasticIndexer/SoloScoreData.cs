// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace osu.ElasticIndexer
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("Style", "IDE1006")]
    public class SoloScoreData
    {
        public int? build_id { get; set; }

        [DefaultValue(false)]
        public bool passed { get; set; }

        [DefaultValue(0)]
        public int total_score { get; set; }

        [DefaultValue(0d)]
        public double accuracy { get; set; }

        [DefaultValue(0)]
        public int max_combo { get; set; }

        public string? rank { get; set; }

        public DateTimeOffset? started_at { get; set; }

        public DateTimeOffset? ended_at { get; set; }

        public JToken? mods { get; set; }
    }
}
