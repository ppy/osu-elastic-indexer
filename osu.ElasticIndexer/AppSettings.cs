// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Nest;

namespace osu.ElasticIndexer
{
    public class AppSettings
    {
        public static readonly IImmutableList<string> VALID_MODES = ImmutableList.Create("osu", "mania", "taiko", "fruits");

        // shared client without a default index.
        internal static readonly ElasticClient ELASTIC_CLIENT;

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

            if (!string.IsNullOrEmpty(config["concurrency"]))
                Concurrency = int.Parse(config["concurrency"]);

            if (!string.IsNullOrEmpty(config["chunk_size"]))
                ChunkSize = int.Parse(config["chunk_size"]);

            if (!string.IsNullOrEmpty(config["buffer_size"]))
                BufferSize = int.Parse(config["buffer_size"]);

            if (!string.IsNullOrEmpty(config["resume_from"]))
                ResumeFrom = ulong.Parse(config["resume_from"]);

            if (!string.IsNullOrEmpty(config["polling_interval"]))
                PollingInterval = int.Parse(config["polling_interval"]);

            var modesStr = config["modes"] ?? string.Empty;
            Modes = modesStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Intersect(VALID_MODES).ToImmutableArray();

            ConnectionString = config.GetConnectionString("osu");
            IsNew = parseBool("new");
            IsPrepMode = parseBool("prep");
            IsRebuild = IsPrepMode || parseBool("rebuild");
            IsWatching = parseBool("watch");

            Prefix = config["elasticsearch:prefix"];
            Schema = config["schema"];

            ElasticsearchHost = config["elasticsearch:host"];
            ElasticsearchPrefix = config["elasticsearch:prefix"];

            ELASTIC_CLIENT = new ElasticClient(new ConnectionSettings(new Uri(ElasticsearchHost)));

            UseDocker = parseBool("docker");

            assertOptionsCompatible();
        }

        public static int BufferSize { get; private set; } = 5;

        // same value as elasticsearch-net
        public static TimeSpan BulkAllBackOffTimeDefault = TimeSpan.FromMinutes(1);

        public static int ChunkSize { get; private set; } = 10000;

        public static int Concurrency { get; private set; } = 4;

        public static string ConnectionString { get; private set; }

        public static string ElasticsearchHost { get; private set; }

        public static string ElasticsearchPrefix { get; private set; }

        public static bool IsNew { get; set; }

        public static bool IsPrepMode { get; set; }

        public static bool IsRebuild { get; private set; }

        public static bool IsWatching { get; private set; }

        public static ImmutableArray<string> Modes { get; private set; }

        public static int PollingInterval { get; private set; } = 10000;

        public static string Prefix { get; private set; }

        public static ulong? ResumeFrom { get; private set; }

        public static bool UseDocker { get; private set; }

        public static string Schema { get; private set; }

        private static void assertOptionsCompatible()
        {
            if (IsRebuild && IsWatching)
                throw new Exception("watch mode cannot be used with index rebuilding.");

            if (!IsRebuild)
            {
                if (string.IsNullOrWhiteSpace(Schema))
                    throw new Exception("queue processing requires a schema version to be specified.");

                if (IsNew)
                    throw new Exception("creating a new index is not supported with queue processing.");
            }

            if (IsPrepMode)
            {
                if (!IsRebuild)
                    throw new Exception("prep mode is only valid while rebuilding.");

                if (string.IsNullOrWhiteSpace(Schema))
                    throw new Exception("rebuilding in prep mode requires a schema version.");

                if (ResumeFrom.HasValue)
                    throw new Exception("resume_from cannot be used in this mode.");
            }
        }

        private static bool parseBool(string key)
        {
            return new[] { "1", "true" }.Contains((config[key] ?? string.Empty).ToLowerInvariant());
        }
    }
}
