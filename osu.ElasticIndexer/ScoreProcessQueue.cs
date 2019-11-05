// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Dapper;
using Dapper.Contrib.Extensions;
using MySql.Data.MySqlClient;

namespace osu.ElasticIndexer
{
    [CursorColumn("queue_id")]
    [Table("score_process_queue")]
    public class ScoreProcessQueue : Model
    {
        public override ulong CursorValue => QueueId;

        // These are the only columns we care about at the momemnt.
        public uint QueueId { get; set; }

        public ulong ScoreId { get; set; }

        public static List<T> FetchByScoreIds<T>(List<ulong> scoreIds) where T : HighScore
        {
            var table = GetTableName<T>();

            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                dbConnection.Open();
                return dbConnection.Query<T>($"select * from {table} where score_id in @scoreIds", new { scoreIds }).AsList();
            }
        }

        public static ulong? GetLastProcessedQueueId(int mode)
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                dbConnection.Open();
                return dbConnection.QuerySingleOrDefault<ulong?>("SELECT queue_id FROM score_process_queue WHERE status = 2 AND mode = @mode order by queue_id DESC LIMIT 1", new { mode });
            }
        }

        public static void CompleteQueued(List<ScoreProcessQueue> queueItems)
        {
            if (!queueItems.Any()) return;
            var queueIds = queueItems.Select(x => x.QueueId);

            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                dbConnection.Open();
                dbConnection.Execute("update score_process_queue set status = 2 where queue_id in @queueIds", new { queueIds });
            }
        }

        public static void UnCompleteQueued<T>(ulong from) where T : HighScore
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                var mode = HighScore.GetRulesetId<T>();

                dbConnection.Open();
                dbConnection.Execute("UPDATE score_process_queue SET status = 1 WHERE queue_id > @from AND mode = @mode", new { from, mode });
            }
        }
    }
}
