// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapper;
using Dapper.Contrib.Extensions;
using MySql.Data.MySqlClient;

namespace osu.ElasticIndexer
{
    [CursorColumn("queue_id")]
    [Table("score_process_queue")]
    public class ScoreProcessQueue : Model
    {
        public override long CursorValue => QueueId;

        // These are the only columns we care about at the momemnt.
        public uint QueueId { get; set; }

        public ulong ScoreId { get; set; }

        public static List<T> FetchIds<T>(List<ScoreProcessQueue> items) where T : HighScore
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                var scoreIds = items.Select(x => x.ScoreId).AsList();
                var table = typeof(T).GetCustomAttributes<TableAttribute>().First().Name;

                dbConnection.Open();

                string query = $"select * from {table} where score_id in @scoreIds";
                var parameters = new { scoreIds };
                Console.WriteLine("{0} {1}", query, parameters);

                return dbConnection.Query<T>(query, parameters).AsList();
            }
        }

        public static void CompleteQueued<T>(List<ScoreProcessQueue> items) where T : HighScore
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                var scoreIds = items.Select(x => x.ScoreId).AsList();
                var mode = typeof(T).GetCustomAttributes<RulesetIdAttribute>().First().Id;

                if (!scoreIds.Any()) return;

                dbConnection.Open();

                const string query = "update score_process_queue set status = 2 where score_id in @scoreIds and mode = @mode";
                dbConnection.Execute(query, new { scoreIds, mode });
            }
        }
    }
}
