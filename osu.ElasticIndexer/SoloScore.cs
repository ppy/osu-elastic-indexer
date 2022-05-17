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
    [ElasticsearchType(IdProperty = nameof(Id))]
    [ChunkOn(
        Query = "s.*, p.pp from solo_scores s left join solo_scores_performance p on s.id = p.score_id",
        CursorColumn = "s.id",
        Max = "MAX(id) FROM solo_scores"
    )]
    [Table("solo_scores")]
    public class SoloScore : Model
    {
        [Computed]
        [Ignore]
        public override long CursorValue => Id;

        [Computed]
        [Ignore]
        public bool ShouldIndex => preserve;

        // Properties ordered in the order they appear in the table.

        [Number(NumberType.Long, Name = "id")]
        public long Id { get; set; }

        [Number(NumberType.Long, Name = "beatmap_id")]
        public uint BeatmapId { get; set; }

        [Number(NumberType.Long, Name = "user_id")]
        public uint UserId { get; set; }

        [Number(NumberType.Short, Name = "ruleset_id")]
        public int RulesetId { get; set; }
        [Date(Name = "created_at", Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset CreatedAt { get; set; }

        [Date(Name = "updated_at", Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset UpdatedAt { get; set; }

        [Ignore]
        public string Data { get; set; } = String.Empty;

        [Computed]
        [Number(NumberType.Integer)]
        public int? build_id { get => (int?)scoreInfo.Value.GetValueOrDefault("build_id"); }

        [Computed]
        [Boolean]
        public bool passed { get => scoreInfo.Value["passed"]; }

        [Number(NumberType.Float)]
        public double? pp { get; set; }

        public bool preserve { get; set; }

        [Computed]
        [Number(NumberType.Integer)]
        public int total_score { get => (int)scoreInfo.Value["total_score"]; }

        [Computed]
        [Number(NumberType.Float)]
        public double accuracy { get => scoreInfo.Value["accuracy"]; }

        [Computed]
        [Number(NumberType.Integer)]
        public int max_combo { get => (int)scoreInfo.Value["max_combo"]; }

        [Computed]
        [Keyword]
        public string rank { get => scoreInfo.Value["rank"]; }

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? started_at { get => scoreInfo.Value.GetValueOrDefault("started_at"); }

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? ended_at { get => scoreInfo.Value.GetValueOrDefault("ended_at"); }

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
        public string country_code { get; set; } = "";

        private Lazy<Dictionary<string, dynamic>> scoreInfo;

        public SoloScore()
        {
            scoreInfo = new Lazy<Dictionary<string, dynamic>>(() =>
                JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(Data) ?? new Dictionary<string, dynamic>()
            );
        }

        public override string ToString() => $"score_id: {Id} user_id: {UserId} beatmap_id: {BeatmapId} ruleset_id: {RulesetId}";
    }
}
