// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace osu.ElasticIndexer
{
    public class AppSettings
    {
        // TODO: readonly
        public static readonly IImmutableList<string> VALID_MODES = ImmutableList.Create("osu", "mania", "taiko", "fruits");

        private static IConfigurationRoot config;

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

            IsNew = parseBool("new");

            ChunkSize = string.IsNullOrEmpty(config["chunk_size"])
                        ? 10000
                        : int.Parse(config["chunk_size"]);

            ConnectionString = config.GetConnectionString("osu");

            if (!string.IsNullOrEmpty(config["queue_size"]))
                QueueSize = int.Parse(config["queue_size"]);

            if (!string.IsNullOrEmpty(config["resume_from"]))
                ResumeFrom = long.Parse(config["resume_from"]);

            IsWatching = parseBool("watch");
            PollingInterval = string.IsNullOrEmpty(config["polling_interval"])
                              ? 10000
                              : int.Parse(config["polling_interval"]);

            Prefix = config["elasticsearch:prefix"];

            var modesStr = config["modes"] ?? string.Empty;
            Modes = modesStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Intersect(VALID_MODES).ToImmutableArray();

            ElasticsearchHost = config["elasticsearch:host"];
            ElasticsearchPrefix = config["elasticsearch:prefix"];
        }

        public static int ChunkSize { get; private set; }

        public static string ConnectionString { get; private set; }

        public static string ElasticsearchHost { get; private set; }

        public static string ElasticsearchPrefix { get; private set; }

        public static bool IsNew { get; private set; }

        public static bool IsWatching { get; private set; }

        public static ImmutableArray<string> Modes { get; private set; }

        public static int PollingInterval { get; private set; }

        public static string Prefix { get; private set; }

        public static int QueueSize { get; private set; } = 5;

        public static long? ResumeFrom { get; private set; }

        private static bool parseBool(string key)
        {
            return new [] { "1", "true" }.Contains((config[key] ?? string.Empty).ToLowerInvariant());
        }
    }
}
