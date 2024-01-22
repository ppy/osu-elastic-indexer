// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper.Contrib.Extensions;
using Nest;
using Newtonsoft.Json;
using osu.Game.Online.API;

namespace osu.ElasticIndexer
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("Style", "IDE1006")]
    [ElasticsearchType(IdProperty = nameof(id))]
    [ChunkOn(
        Query = @"s.*, country_acronym AS country_code, playmode, user_warnings FROM scores s
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
        [JsonIgnore]
        [Ignore]
        public string data
        {
            set => mods = JsonConvert.DeserializeObject<ScoreData>(value)!.Mods.Select(m => m.Acronym).ToList();
        }

        public ushort ruleset_id { get; set; }

        [Computed]
        [Boolean]
        [JsonIgnore]
        public bool convert => ruleset_id != playmode;

        [Ignore]
        public int playmode { get; set; }

        [Number(NumberType.Float)]
        public double? pp { get; set; }

        public bool preserve { get; set; }

        [Number(NumberType.Integer)]
        public int total_score { get; set; } // total score should never exceed int.MaxValue at the point of storage.

        [Number(NumberType.Integer)]
        public int legacy_total_score { get; set; }

        [Number(NumberType.Float)]
        public double accuracy { get; set; }

        [Number(NumberType.Integer)]
        public int max_combo { get; set; }

        [Keyword]
        public string rank { get; set; } = string.Empty;

        [Ignore]
        public int user_warnings { get; set; }

        [Keyword]
        public List<string> mods { get; set; } = null!;

        [Keyword]
        public string? country_code { get; set; }

        [Boolean]
        public bool is_legacy => legacy_score_id != null;

        [Ignore]
        [JsonIgnore]
        public ulong? legacy_score_id { get; set; }

        public override string ToString() => $"score_id: {id} user_id: {user_id} beatmap_id: {beatmap_id} ruleset_id: {ruleset_id}";

        /// <summary>
        /// Minimal implementation of SoloScoreInfo (aka the json serialised content of `scores`.`data`).
        /// </summary>
        [Serializable]
        public class ScoreData
        {
            [JsonProperty("mods")]
            public APIMod[] Mods { get; set; } = Array.Empty<APIMod>();
        }
    }
}
