// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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

        public static void CompleteQueued<T>(List<ulong> scoreIds) where T : HighScore
        {
            if (!scoreIds.Any()) return;

            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                var mode = HighScore.GetRulesetId<T>();

                dbConnection.Open();

                const string query = "update score_process_queue set status = 2 where score_id in @scoreIds and mode = @mode";
                dbConnection.Execute(query, new { scoreIds, mode });
            }
        }

        public static List<T> FetchByScoreIds<T>(List<ulong> scoreIds) where T : HighScore
        {
            var table = Model.GetTableName<T>();

            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                dbConnection.Open();

                string query = $"select * from {table} where score_id in @scoreIds";
                var parameters = new { scoreIds };
                Console.WriteLine("{0} {1}", query, parameters);

                return dbConnection.Query<T>(query, parameters).AsList();
            }
        }

        public static ulong? GetLastProcessedQueueId(int mode)
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                dbConnection.Open();

                const string query = "SELECT MAX(queue_id) FROM score_process_queue WHERE status = 2 AND mode = @mode";
                return dbConnection.QuerySingle<ulong?>(query, new { mode });
            }
        }

        public static void UnCompleteQueued<T>(ulong from) where T : HighScore
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                var mode = HighScore.GetRulesetId<T>();

                dbConnection.Open();

                const string query = "UPDATE score_process_queue SET status = 1 WHERE queue_id > @from AND mode = @mode";
                dbConnection.Execute(query, new { from, mode });
            }
        }
    }
}
