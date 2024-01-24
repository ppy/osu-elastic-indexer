// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MySqlConnector;
using StatsdClient;

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

        /// <summary>
        /// A set of all score IDs which have arrived but are not yet determined to be an addition or deletion.
        /// These should be processed into either <see cref="Additions"/> or <see cref="Deletions"/>.
        /// </summary>
        private readonly HashSet<long> scoreIdsForLookup = new HashSet<long>();

        public ProcessableItemsBuffer(MySqlConnection connection, IEnumerable<ScoreQueueItem> items)
        {
            // Figure out what to do with the queue item.
            foreach (var item in items)
            {
                if (item.ScoreId != null)
                {
                    scoreIdsForLookup.Add(item.ScoreId.Value);
                }
                else if (item.Score != null)
                {
                    if (item.Score.ShouldIndex)
                        Additions.Add(item.Score);
                    else
                        Deletions.Add(item.Score.id);
                }
                else
                {
                    Console.WriteLine(ConsoleColor.Red, "queue item missing both data and action");
                }
            }

            // Handle any scores that need a lookup from the database.
            if (scoreIdsForLookup.Any())
            {
                var scores = ElasticModel.Find<Score>(connection, scoreIdsForLookup);

                foreach (var score in scores)
                {
                    if (score.ShouldIndex)
                    {
                        DogStatsd.Increment("indexed", 1, 1, new[] { "action:add", $"type:{(score.is_legacy ? "legacy" : "normal")}", $"ruleset:{score.ruleset_id}" });
                        Additions.Add(score);
                    }
                    else
                    {
                        DogStatsd.Increment("indexed", 1, 1, new[] { "action:remove", $"type:{(score.is_legacy ? "legacy" : "normal")}", $"ruleset:{score.ruleset_id}" });
                        Deletions.Add(score.id);
                    }

                    scoreIdsForLookup.Remove(score.id);
                }

                // Remaining scores do not exist and should be deleted.
                Deletions.AddRange(scoreIdsForLookup);
                scoreIdsForLookup.Clear();
            }

            Debug.Assert(scoreIdsForLookup.Count == 0);
        }
    }
}
