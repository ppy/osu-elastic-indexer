// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace osu.ElasticIndexer
{
    public class ProcessableItemsBuffer
    {
        /// <summary>
        /// New scores which should be indexed.
        /// </summary>
        public readonly List<Score> Additions = new List<Score>();

        /// <summary>
        /// Score IDs which should be purged from the index is they are present.
        /// </summary>
        public readonly List<long> Deletions = new List<long>();

        public ProcessableItemsBuffer(MySqlConnection connection, IEnumerable<ScoreQueueItem> items)
        {
            Dictionary<long, Score> scores = ElasticModel.Find<Score>(connection, items.Select(i => i.ScoreId)).ToDictionary(s => s.id, s => s);

            foreach (var item in items)
            {
                if (scores.TryGetValue(item.ScoreId, out var score))
                {
                    item.Tags = (item.Tags ?? Array.Empty<string>()).Concat(new[] { "action:add", $"type:{(score.is_legacy ? "legacy" : "normal")}", $"ruleset:{score.ruleset_id}" }).ToArray();
                    Additions.Add(score);
                }
                else
                {
                    item.Tags = (item.Tags ?? Array.Empty<string>()).Append("action:remove").ToArray();
                    Deletions.Add(item.ScoreId);
                }
            }
        }
    }
}
