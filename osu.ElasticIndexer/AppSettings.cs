// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
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
            var env = Environment.GetEnvironmentVariable("APP_ENV") ?? "development";
            var config = new ConfigurationBuilder()
                         .SetBasePath(Directory.GetCurrentDirectory())
                         .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                         .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
                         .AddEnvironmentVariables()
                         .Build();

            if (!string.IsNullOrEmpty(config["batch_size"]))
                BatchSize = int.Parse(config["batch_size"]);

            if (!string.IsNullOrEmpty(config["buffer_size"]))
                BufferSize = int.Parse(config["buffer_size"]);

            ConnectionString = config.GetConnectionString("osu");
            Schema = config["schema"] ?? string.Empty;
            Prefix = config["prefix"] ?? string.Empty;
            ElasticsearchHost = config["elasticsearch:host"];
            RedisHost = config["redis:host"] ?? "redis";

            // set env variable for queue processor.
            Environment.SetEnvironmentVariable("REDIS_HOST", RedisHost);
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
