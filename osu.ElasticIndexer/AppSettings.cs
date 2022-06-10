// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Microsoft.Extensions.Configuration;

namespace osu.ElasticIndexer
{
    public class AppSettings
    {
        private AppSettings()
        {
        }

        static AppSettings()
        {
            var config = new ConfigurationBuilder()
                         .AddEnvironmentVariables()
                         .Build();

            if (!string.IsNullOrEmpty(config["batch_size"]))
                BatchSize = int.Parse(config["batch_size"]);

            if (!string.IsNullOrEmpty(config["buffer_size"]))
                BufferSize = int.Parse(config["buffer_size"]);

            ConnectionString = config["DB_CONNECTION_STRING"];
            Schema = config["schema"] ?? string.Empty;
            Prefix = config["prefix"] ?? string.Empty;
            ElasticsearchHost = config["ES_HOST"];
            RedisHost = config["REDIS_HOST"];
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
