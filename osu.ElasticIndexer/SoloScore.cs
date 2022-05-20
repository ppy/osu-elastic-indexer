// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper.Contrib.Extensions;
using Nest;
using Newtonsoft.Json;

namespace osu.ElasticIndexer
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("Style", "IDE1006")]
    [ElasticsearchType(IdProperty = nameof(id))]
    [ChunkOn(
        Query = "s.*, (select pp from solo_scores_performance p where p.score_id = s.id) pp from solo_scores s",
        CursorColumn = "s.id",
        Max = "MAX(id) FROM solo_scores"
    )]
    [Table("solo_scores")]
    public class SoloScore : Model
    {
        public override long CursorValue => id;

        [Computed]
        [Ignore]
        [JsonIgnore]
        public bool ShouldIndex => preserve;

        // Properties ordered in the order they appear in the table.

        [Number(NumberType.Long)]
        public long id { get; set; }

        [Number(NumberType.Long)]
        public uint beatmap_id { get; set; }

        [Number(NumberType.Long)]
        public uint user_id { get; set; }

        [Number(NumberType.Short)]
        public int ruleset_id { get; set; }

        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset created_at { get; set; }

        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset updated_at { get; set; }

        [Ignore]
        public string data { get; set; } = string.Empty;

        [Computed]
        [Number(NumberType.Integer)]
        public int? build_id => (int?)scoreInfo.Value.GetValueOrDefault("build_id");

        [Computed]
        [Boolean]
        public bool passed => scoreInfo.Value.GetValueOrDefault("passed") ?? false;

        [Number(NumberType.Float)]
        public double? pp { get; set; }

        public bool preserve { get; set; }

        [Computed]
        [Number(NumberType.Integer)]
        public int total_score => (int)(scoreInfo.Value.GetValueOrDefault("total_score") ?? 0);

        [Computed]
        [Number(NumberType.Float)]
        public double accuracy => (double)(scoreInfo.Value.GetValueOrDefault("accuracy") ?? 0);

        [Computed]
        [Number(NumberType.Integer)]
        public int max_combo => (int)(scoreInfo.Value.GetValueOrDefault("max_combo") ?? 0);

        [Computed]
        [Keyword]
        public string rank => scoreInfo.Value.GetValueOrDefault("rank") ?? "F";

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? started_at => scoreInfo.Value.GetValueOrDefault("started_at");

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? ended_at => scoreInfo.Value.GetValueOrDefault("ended_at");

        [Computed]
        [Keyword]
        public List<string> mods
        {
            get
            {
                List<dynamic> mods = scoreInfo.Value["mods"].ToObject<List<dynamic>>();
                return mods.Select(mod => (string)mod["acronym"]).ToList();
            }
        }

        [Computed]
        [Keyword]
        public string country_code { get; set; } = string.Empty;

        private Lazy<Dictionary<string, dynamic>> scoreInfo;

        public SoloScore()
        {
            scoreInfo = new Lazy<Dictionary<string, dynamic>>(() =>
                JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(data) ?? new Dictionary<string, dynamic>()
            );
        }

        public override string ToString() => $"score_id: {id} user_id: {user_id} beatmap_id: {beatmap_id} ruleset_id: {ruleset_id}";
    }
}
