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
    public class OsuElasticClient : ElasticClient
    {
        public readonly string AliasName = $"{AppSettings.Prefix}scores";

        public OsuElasticClient(bool throwsExceptions = true)
            : base(new ConnectionSettings(new Uri(AppSettings.ElasticsearchHost))
                   .EnableApiVersioningHeader()
                   .ThrowExceptions(throwsExceptions))
        {
        }

        /// <summary>
        /// Attempts to find the matching index or creates a new one.
        /// </summary>
        /// <param name="name">name of the index alias.</param>
        /// <returns>Name of index found or created and any existing alias.</returns>
        public IndexMetadata FindOrCreateIndex(string name)
        {
            Console.WriteLine();

            var index = GetIndexForSchema(name);

            if (index != null)
            {
                var (indexName, indexState) = index.Value;
                Console.WriteLine(ConsoleColor.Cyan, $"Using existing index `{indexName}`.");
                return new IndexMetadata(indexName, indexState);
            }

            return createIndex(name);
        }

        public IReadOnlyDictionary<IndexName, IndexState> GetIndex(string name)
        {
            return Indices.Get(name).Indices;
        }

        public IReadOnlyDictionary<IndexName, IndexState> GetIndices(string name, ExpandWildcards expandWildCards = ExpandWildcards.Open)
        {
            return Indices.Get(name, descriptor => descriptor.ExpandWildcards(expandWildCards)).Indices;
        }

        public KeyValuePair<IndexName, IndexState>? GetIndexForSchema(string schema)
        {
            KeyValuePair<IndexName, IndexState>? index = GetIndices($"{AliasName}_{schema}")
                .SingleOrDefault();

            if (string.IsNullOrEmpty(index?.Key?.Name))
                return null;

            return index;
        }

        public void UpdateAlias(string alias, string index, bool close = true)
        {
            // TODO: updating alias should mark the index as ready since it's switching over.
            Console.WriteLine(ConsoleColor.Yellow, $"Updating `{alias}` alias to `{index}`...");

            var aliasDescriptor = new BulkAliasDescriptor();
            var oldIndices = this.GetIndicesPointingToAlias(alias);

            foreach (var oldIndex in oldIndices)
                aliasDescriptor.Remove(d => d.Alias(alias).Index(oldIndex));

            aliasDescriptor.Add(d => d.Alias(alias).Index(index));
            Indices.BulkAlias(aliasDescriptor);

            // cleanup
            if (!close) return;

            foreach (var toClose in oldIndices.Where(x => x != index))
            {
                Console.WriteLine(ConsoleColor.Yellow, $"Closing {toClose}");
                Indices.Close(toClose);
            }
        }

        private IndexMetadata createIndex(string schema)
        {
            string name = $"{AliasName}_{schema}";

            Console.WriteLine(ConsoleColor.Cyan, $"Creating new index `{name}`.");

            var json = File.ReadAllText(Path.GetFullPath("schemas/scores.json"));
            LowLevel.Indices.Create<DynamicResponse>(
                name,
                json,
                new CreateIndexRequestParameters { WaitForActiveShards = "all" }
            );
            var metadata = new IndexMetadata(name, AppSettings.Schema);

            metadata.Save(this);

            return metadata;
        }
    }
}
