// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    public class AppSettings
    {
        private AppSettings()
        {
        }

        static AppSettings()
        {
            var batchSizeEnv = Environment.GetEnvironmentVariable("batch_size");
            if (!string.IsNullOrEmpty(batchSizeEnv))
                BatchSize = int.Parse(batchSizeEnv);

            var bufferSizeEnv = Environment.GetEnvironmentVariable("batch_size");
            if (!string.IsNullOrEmpty(bufferSizeEnv))
                BufferSize = int.Parse(bufferSizeEnv);

            ConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? string.Empty;
            Schema = Environment.GetEnvironmentVariable("schema") ?? string.Empty;
            Prefix = Environment.GetEnvironmentVariable("prefix") ?? string.Empty;
            ElasticsearchHost = Environment.GetEnvironmentVariable("ES_HOST") ?? "http://elasticsearch:9200";
            RedisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "redis";
        }

        public static int BufferSize { get; } = 5;

        // same value as elasticsearch-net
        public static TimeSpan BulkAllBackOffTimeDefault = TimeSpan.FromMinutes(1);

        public static int BatchSize { get; } = 10000;

        public static string ConnectionString { get; }

        public static string ElasticsearchHost { get; }

        public static string Prefix { get; }

        public static string RedisHost { get; }

        public static string Schema { get; }
    }
}
