// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper.Contrib.Extensions;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using osu.Game.Online.API;
using osu.Game.Scoring;

namespace osu.ElasticIndexer
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("Style", "IDE1006")]
    [ElasticsearchType(IdProperty = nameof(id))]
    [ChunkOn(
        Query = @"s.*, pp, country_acronym AS country_code, playmode, user_warnings FROM scores s
        LEFT JOIN scores_performance ON score_id = s.id
        JOIN phpbb_users USING (user_id)
        JOIN osu_beatmaps USING (beatmap_id)",
        CursorColumn = "s.id",
        Max = "MAX(id) FROM scores"
    )]
    [Table("scores")]
    public class Score : ElasticModel
    {
        public override long CursorValue => id;

        [Computed]
        [Ignore]
        [JsonIgnore]
        public bool ShouldIndex => preserve && user_warnings == 0;

        // Properties ordered in the order they appear in the table.

        [Number(NumberType.Long)]
        public long id { get; set; }

        [Keyword]
        public uint beatmap_id { get; set; }

        [Keyword]
        public uint user_id { get; set; }

        [Keyword]
        public int ruleset_id { get; set; }

        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset created_at { get; set; }

        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset updated_at { get; set; }

        [JsonIgnore]
        [Ignore]
        public string data
        {
            set => scoreData = JsonConvert.DeserializeObject<ScoreData>(value)!;
        }

        [Computed]
        [Keyword]
        public int? build_id => scoreData.BuildID;

        [Computed]
        [Boolean]
        [JsonIgnore]
        public bool convert => ruleset_id != playmode;

        [Computed]
        [Boolean]
        public bool passed => scoreData.Passed;

        [Ignore]
        public int playmode { get; set; }

        [Number(NumberType.Float)]
        public double? pp { get; set; }

        public bool preserve { get; set; }

        [Computed]
        [Number(NumberType.Integer)]
        public int total_score => scoreData.LegacyTotalScore ?? (int)scoreData.TotalScore; // scoreData.TotalScore should never exceed int.MaxValue at the point of storage.

        [Computed]
        [Number(NumberType.Float)]
        public double accuracy => scoreData.Accuracy;

        [Computed]
        [Number(NumberType.Integer)]
        public int max_combo => scoreData.MaxCombo;

        [Computed]
        [Keyword]
        public string rank => scoreData.Rank.ToString();

        [Ignore]
        public int user_warnings { get; set; }

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? started_at => scoreData.StartedAt;

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? ended_at => scoreData.EndedAt;

        [Computed]
        [Keyword]
        public List<string> mods => scoreData.Mods.Select(mod => mod.Acronym).ToList();

        [Computed]
        [Keyword]
        public string? country_code { get; set; }

        [Computed]
        [Boolean]
        public bool is_legacy => build_id == null;

        public ScoreData scoreData = new ScoreData();

        public override string ToString() => $"score_id: {id} user_id: {user_id} beatmap_id: {beatmap_id} ruleset_id: {ruleset_id}";

        /// <summary>
        /// Minimal implementation of SoloScoreInfo (aka the json serialised content of `scores`.`data`).
        /// </summary>
        [Serializable]
        public class ScoreData
        {
            [JsonProperty("build_id")]
            public int? BuildID { get; set; }

            [JsonProperty("passed")]
            public bool Passed { get; set; }

            [JsonProperty("total_score")]
            public long TotalScore { get; set; }

            [JsonProperty("accuracy")]
            public double Accuracy { get; set; }

            [JsonProperty("max_combo")]
            public int MaxCombo { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            // ScoreRank is aligned to make 0 equal D. We still want to serialise this (even when DefaultValueHandling.Ignore is used).
            [JsonProperty("rank", DefaultValueHandling = DefaultValueHandling.Include)]
            public ScoreRank Rank { get; set; }

            [JsonProperty("started_at")]
            public DateTimeOffset? StartedAt { get; set; }

            [JsonProperty("ended_at")]
            public DateTimeOffset EndedAt { get; set; }

            [JsonProperty("mods")]
            public APIMod[] Mods { get; set; } = Array.Empty<APIMod>();

            /// <summary>
            /// Used to preserve the total score for legacy scores.
            /// </summary>
            [JsonProperty("legacy_total_score")]
            public int? LegacyTotalScore { get; set; }
        }
    }
}
