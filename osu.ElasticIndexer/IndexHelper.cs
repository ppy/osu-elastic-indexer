// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elasticsearch.Net;
using Elasticsearch.Net.Specification.IndicesApi;
using Nest;

namespace osu.ElasticIndexer
{
    public class IndexHelper
    {
        /// <summary>
        /// Attempts to find the matching index or creates a new one.
        /// </summary>
        /// <param name="name">name of the index alias.</param>
        /// <returns>Name of index found or created and any existing alias.</returns>
        public static Metadata FindOrCreateIndex(string name)
        {
            Console.WriteLine();

            var indices = IndexHelper.GetIndicesForCurrentVersion(name);

            if (indices.Count > 0 && !AppSettings.IsNew)
            {
                // 3 cases are handled:
                // 1. Index was already aliased and has tracking information; likely resuming from a completed job.
                var (indexName, indexState) = indices.FirstOrDefault(entry => entry.Value.Aliases.ContainsKey(name));
                if (indexName != null)
                {
                    Console.WriteLine($"Using aliased `{indexName}`.");

                    return new Metadata(indexName, indexState) { IsAliased = true };
                }

                // 2. Index has not been aliased and has tracking information;
                // likely resuming from an incomplete job or waiting to switch over.
                // TODO: throw if there's more than one? or take lastest one.
                (indexName, indexState) = indices.First();
                Console.WriteLine($"Using non-aliased `{indexName}`.");

                return new Metadata(indexName, indexState);
            }

            if (indices.Count == 0 && AppSettings.IsWatching)
                throw new Exception("no existing index found");

            // 3. Not aliased and no tracking information; likely starting from scratch
            var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            var index = $"{name}_{suffix}";

            Console.WriteLine($"Creating `{index}` for `{name}`.");

            // create by supplying the json file instead of the attributed class because we're not
            // mapping every field but still want everything for _source.
            var json = File.ReadAllText(Path.GetFullPath("schemas/solo_scores.json"));
            AppSettings.ELASTIC_CLIENT.LowLevel.Indices.Create<DynamicResponse>(
                index,
                json,
                new CreateIndexRequestParameters() { WaitForActiveShards = "all" }
            );
            var metadata = new Metadata(index, AppSettings.Schema);
            metadata.Save();

            return metadata;

            // TODO: cases not covered should throw an Exception (aliased but not tracked, etc).
        }

        public static List<KeyValuePair<IndexName, IndexState>> GetIndicesForCurrentVersion(string name)
        {
            return AppSettings.ELASTIC_CLIENT.Indices.Get($"{name}_*").Indices
                .Where(entry => (string) entry.Value.Mappings.Meta?["schema"] == AppSettings.Schema)
                .ToList();
        }

        public static void UpdateAlias(string alias, string index, bool close = true)
        {
            // TODO: updating alias should mark the index as ready since it's switching over.
            Console.WriteLine($"Updating `{alias}` alias to `{index}`...");

            var aliasDescriptor = new BulkAliasDescriptor();
            var oldIndices = AppSettings.ELASTIC_CLIENT.GetIndicesPointingToAlias(alias);

            foreach (var oldIndex in oldIndices)
                aliasDescriptor.Remove(d => d.Alias(alias).Index(oldIndex));

            aliasDescriptor.Add(d => d.Alias(alias).Index(index));
            AppSettings.ELASTIC_CLIENT.Indices.BulkAlias(aliasDescriptor);

            // cleanup
            if (!close) return;
            foreach (var toClose in oldIndices.Where(x => x != index))
            {
                Console.WriteLine($"Closing {toClose}");
                AppSettings.ELASTIC_CLIENT.Indices.Close(toClose);
            }
        }
    }
}
