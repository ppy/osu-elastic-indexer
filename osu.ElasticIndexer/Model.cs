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
    [CursorColumn("id")]
    public abstract class Model
    {
        public abstract long CursorValue { get; }

        public static IEnumerable<List<T>> Chunk<T>(string where, int chunkSize = 10000, long? resumeFrom = null) where T : Model
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                long? lastId = resumeFrom ?? 0;
                Console.WriteLine($"Starting from {lastId}...");

                var cursorColumn = typeof(T).GetCustomAttributes<CursorColumnAttribute>().First().Name;
                var table = typeof(T).GetCustomAttributes<TableAttribute>().First().Name;

                // FIXME: this is terrible.
                var additionalWheres = string.IsNullOrWhiteSpace(where) ? "" : $"AND {where}";

                dbConnection.Open();

                string query = $"select * from {table} where {cursorColumn} > @lastId {additionalWheres} order by {cursorColumn} asc limit @chunkSize;";

                while (lastId != null)
                {
                    var parameters = new { lastId, chunkSize };
                    Console.WriteLine("{0} {1}", query, parameters);
                    var queryResult = dbConnection.Query<T>(query, parameters).AsList();

                    lastId = queryResult.LastOrDefault()?.CursorValue;
                    if (lastId.HasValue) yield return queryResult;
                }
            }
        }

        public static List<T> FetchQueued<T>() where T : Model
        {
            // probably does not need chunking?
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                var table = typeof(T).GetCustomAttributes<TableAttribute>().First().Name;
                var mode = typeof(T).GetCustomAttributes<RulesetId>().First().Id;

                dbConnection.Open();

                string queueQuery = $"select score_id from score_process_queue where status = 1 and mode = @mode";
                var scoreIds = dbConnection.Query<long>(queueQuery, new { mode }).AsList();

                Console.WriteLine($"{scoreIds.Count} score_ids found.");
                if (scoreIds.Count > 0)
                {
                    string query = $"select * from {table} where score_id in @scoreIds";
                    var records = dbConnection.Query<T>(query, new { scoreIds }).AsList();
                    Console.WriteLine($"{records.Count} records selected.");

                    return records;
                }

                Console.WriteLine("no records selected.");
                return new List<T>(0);
            }
        }

        public static void CompleteQueued<T>(List<T> models) where T : Model
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                var table = typeof(T).GetCustomAttributes<TableAttribute>().First().Name;
                var mode = typeof(T).GetCustomAttributes<RulesetId>().First().Id;
                var scoreIds = models.Select(x => x.CursorValue); // TODO: should change to ScoreId

                dbConnection.Open();

                if (scoreIds.Count() > 0)
                {
                    string query = $"update score_process_queue set status = 2 where score_id in @scoreIds and mode = @mode";
                    dbConnection.Execute(query, new { scoreIds, mode });
                }

            }
        }

        public static IEnumerable<List<T>> Chunk<T>(int chunkSize = 10000, long? resumeFrom = null) where T : Model =>
            Chunk<T>(null, chunkSize, resumeFrom);
    }
}
