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
        Query = @"s.*, pp, country_acronym AS country_code, playmode, user_warnings FROM solo_scores s
        LEFT JOIN solo_scores_performance ON score_id = s.id
        JOIN phpbb_users USING (user_id)
        JOIN osu_beatmaps USING (beatmap_id)",
        CursorColumn = "s.id",
        Max = "MAX(id) FROM solo_scores"
    )]
    [Table("solo_scores")]
    public class SoloScore : ElasticModel
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
            get => JsonConvert.SerializeObject(scoreData);
            set
            {
                var obj = JsonConvert.DeserializeObject<SoloScoreData>(value);

                if (obj != null)
                    scoreData = obj;
            }
        }

        [Computed]
        [Keyword]
        public int? build_id => scoreData.build_id;

        [Computed]
        [Boolean]
        [JsonIgnore]
        public bool convert => ruleset_id != playmode;

        [Computed]
        [Boolean]
        public bool passed => scoreData.passed;

        [Ignore]
        public int playmode { get; set; }

        [Number(NumberType.Float)]
        public double? pp { get; set; }

        public bool preserve { get; set; }

        [Computed]
        [Number(NumberType.Integer)]
        public int total_score => scoreData.total_score;

        [Computed]
        [Number(NumberType.Float)]
        public double accuracy => scoreData.accuracy;

        [Computed]
        [Number(NumberType.Integer)]
        public int max_combo => scoreData.max_combo;

        [Computed]
        [Keyword]
        public string? rank => scoreData.rank;

        [Ignore]
        public int user_warnings { get; set; }

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? started_at => scoreData.started_at;

        [Computed]
        [Date(Format = "strict_date_optional_time||epoch_millis||yyyy-MM-dd HH:mm:ss")]
        public DateTimeOffset? ended_at => scoreData.ended_at;

        [Computed]
        [Keyword]
        public List<string> mods
        {
            get
            {
                List<dynamic> modObjects = scoreData.mods?.ToObject<List<dynamic>>() ?? new List<dynamic>();
                return modObjects.Select(mod => (string)mod["acronym"]).ToList();
            }
        }

        [Computed]
        [Keyword]
        public string? country_code { get; set; }

        public SoloScoreData scoreData = new SoloScoreData();

        public override string ToString() => $"score_id: {id} user_id: {user_id} beatmap_id: {beatmap_id} ruleset_id: {ruleset_id}";
    }
}
