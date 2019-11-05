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
        public abstract ulong CursorValue { get; }

        public static IEnumerable<List<T>> Chunk<T>(string where, int chunkSize = 10000, ulong? resumeFrom = null) where T : Model
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                ulong? lastId = resumeFrom ?? 0;
                var cursorColumn = GetCursorColumnName<T>();
                var table = GetTableName<T>();

                Console.WriteLine($"Chunking results from {table} ({where})...");

                dbConnection.Open();

                string maxQuery = $"SELECT MAX({cursorColumn}) FROM {table}";
                if (!string.IsNullOrWhiteSpace(where))
                    maxQuery += $" WHERE {where}";

                var max = dbConnection.QuerySingleOrDefault<ulong?>(maxQuery);
                if (!max.HasValue) yield break;

                // FIXME: this is terrible.
                var additionalWheres = string.IsNullOrWhiteSpace(where) ? "" : $"AND {where}";
                string query = $"select * from {table} where {cursorColumn} > @lastId and {cursorColumn} <= @max {additionalWheres} order by {cursorColumn} asc limit @chunkSize;";

                while (lastId != null)
                {
                    var parameters = new { lastId, max, chunkSize };
                    var queryResult = dbConnection.Query<T>(query, parameters).AsList();

                    lastId = queryResult.LastOrDefault()?.CursorValue;
                    if (lastId.HasValue)
                        yield return queryResult;
                }
            }
        }

        public static IEnumerable<List<T>> Chunk<T>(int chunkSize = 10000, ulong? resumeFrom = null) where T : Model =>
            Chunk<T>(null, chunkSize, resumeFrom);

        public static string GetCursorColumnName<T>() where T : Model
        {
            return typeof(T).GetCustomAttributes<CursorColumnAttribute>().First().Name;
        }

        public static string GetTableName<T>() where T : Model
        {
            return typeof(T).GetCustomAttributes<TableAttribute>().First().Name;
        }
    }
}
