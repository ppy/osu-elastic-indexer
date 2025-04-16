// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    public static class AppSettings
    {
        static AppSettings()
        {
            string? batchSize = Environment.GetEnvironmentVariable("BATCH_SIZE");
            if (!string.IsNullOrEmpty(batchSize))
                BatchSize = int.Parse(batchSize);

            string? bufferSize = Environment.GetEnvironmentVariable("BUFFER_SIZE");
            if (!string.IsNullOrEmpty(bufferSize))
                BufferSize = int.Parse(bufferSize);

            Schema = Environment.GetEnvironmentVariable("SCHEMA") ?? string.Empty;
            Prefix = Environment.GetEnvironmentVariable("ES_INDEX_PREFIX") ?? string.Empty;
            ElasticsearchHost = Environment.GetEnvironmentVariable("ES_HOST") ?? "http://localhost:9200";
            AliasName = $"{Prefix}scores";

            Console.WriteLine(ConsoleColor.Green, string.IsNullOrEmpty(Schema)
                ? "Running without SCHEMA envvar specification"
                : $"Running with SCHEMA envvar specification ({Schema})");

            Console.WriteLine(ConsoleColor.Green, string.IsNullOrEmpty(Prefix)
                ? "Running without ES_INDEX_PREFIX envvar specification"
                : $"Running with ES_INDEX_PREFIX envvar specification ({Prefix})");
        }

        public static int BufferSize { get; } = 5;

        // same value as elasticsearch-net
        public static TimeSpan BulkAllBackOffTimeDefault = TimeSpan.FromMinutes(1);

        public static int BatchSize { get; } = 10000;

        public static string ElasticsearchHost { get; }

        public static string Prefix { get; }

        public static string Schema { get; }

        public static string AliasName { get; }
    }
}
