// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Nest;
using StackExchange.Redis;

namespace osu.ElasticIndexer
{
    public class AppSettings
    {
        // shared client without a default index.
        internal static readonly ElasticClient ELASTIC_CLIENT;

        // TODO: should share with queue processor
        internal static readonly ConnectionMultiplexer Redis;

        private static readonly IConfigurationRoot config;

        private AppSettings()
        {
        }

        static AppSettings()
        {
            var env = Environment.GetEnvironmentVariable("APP_ENV") ?? "development";
            config = new ConfigurationBuilder()
                     .SetBasePath(Directory.GetCurrentDirectory())
                     .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                     .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
                     .AddEnvironmentVariables()
                     .Build();

            // set env variable for queue processor.
            Environment.SetEnvironmentVariable("REDIS_HOST", config["redis:host"] ?? "redis");


            if (!string.IsNullOrEmpty(config["concurrency"]))
                Concurrency = int.Parse(config["concurrency"]);

            if (!string.IsNullOrEmpty(config["chunk_size"]))
                ChunkSize = int.Parse(config["chunk_size"]);

            if (!string.IsNullOrEmpty(config["buffer_size"]))
                BufferSize = int.Parse(config["buffer_size"]);

            ConnectionString = config.GetConnectionString("osu");

            Prefix = config["elasticsearch:prefix"];
            Schema = config["schema"];

            ElasticsearchHost = config["elasticsearch:host"];
            ElasticsearchPrefix = config["elasticsearch:prefix"];

            ELASTIC_CLIENT = new ElasticClient(new ConnectionSettings(new Uri(ElasticsearchHost)));
            Redis = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS_HOST"));
        }

        public static int BufferSize { get; private set; } = 5;

        // same value as elasticsearch-net
        public static TimeSpan BulkAllBackOffTimeDefault = TimeSpan.FromMinutes(1);

        public static int ChunkSize { get; private set; } = 10000;

        public static int Concurrency { get; private set; } = 4;

        public static string ConnectionString { get; private set; }

        public static string ElasticsearchHost { get; private set; }

        public static string ElasticsearchPrefix { get; private set; }

        public static string Prefix { get; private set; }

        public static string Schema { get; private set; }

        private static bool parseBool(string key)
        {
            return new[] { "1", "true" }.Contains((config[key] ?? string.Empty).ToLowerInvariant());
        }
    }
}
