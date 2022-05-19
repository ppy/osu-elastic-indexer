// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapper;
using Dapper.Contrib.Extensions;
using MySqlConnector;
using Nest;
using Newtonsoft.Json;

namespace osu.ElasticIndexer
{
    public abstract class Model
    {
        [Computed]
        [Ignore]
        [JsonIgnore]
        public abstract long CursorValue { get; }

        public static IEnumerable<List<T>> Chunk<T>(string? where, int chunkSize = 10000, long? resumeFrom = null) where T : Model
        {
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                long? lastId = resumeFrom ?? 0;

                var attribute = typeof(T).GetCustomAttributes<ChunkOnAttribute>().First();
                var cursorColumn = attribute.CursorColumn;
                var selects = attribute.Query;
                var maxSelects = attribute.Max;

                Console.WriteLine($"Chunking results from {typeof(T)} ({where})...");

                dbConnection.Open();

                string maxQuery = $"SELECT {maxSelects}";
                if (!string.IsNullOrWhiteSpace(where))
                    maxQuery += $" WHERE {where}";

                var max = dbConnection.QuerySingleOrDefault<long?>(maxQuery);
                if (!max.HasValue) yield break;

                // FIXME: this is terrible.
                var additionalWheres = string.IsNullOrWhiteSpace(where) ? "" : $"AND {where}";
                string query = $"select {selects} where {cursorColumn} > @lastId and {cursorColumn} <= @max {additionalWheres} order by {cursorColumn} asc limit @chunkSize;";

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

        public static IEnumerable<List<T>> Chunk<T>(int chunkSize = 10000, long? resumeFrom = null) where T : Model =>
            Chunk<T>(null, chunkSize, resumeFrom);
    }
}
