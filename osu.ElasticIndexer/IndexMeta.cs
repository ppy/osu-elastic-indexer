// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Nest;

namespace osu.ElasticIndexer
{
    [ElasticsearchType(Name = "index_meta", IdProperty = nameof(Index))]
    public class IndexMeta
    {
        private static readonly ElasticClient client = new ElasticClient(
            new ConnectionSettings(
                new Uri(AppSettings.ElasticsearchHost)
            ).DefaultIndex($"{AppSettings.ElasticsearchPrefix}index_meta")
        );


        /// <summary>
        /// The actual name of the index.
        /// </summary>
        [Text(Name = "index")]
        public string Index { get; set; }

        /// <summary>
        /// The intended alias for the index.
        /// This is used to store the alias that the index should be set to on completion.
        /// </summary>
        [Text(Name = "alias")]
        public string Alias { get; set; }

        [Number(NumberType.Long, Name = "last_id")]
        public ulong LastId { get; set; }

        [Number(NumberType.Long, Name = "reset_queue_to")]
        public ulong? ResetQueueTo { get; set; }

        [Date(Name = "updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        [Text(Name = "version")]
        public string Version { get; set; }

        public static ICreateIndexResponse CreateIndex()
        {
            return client.CreateIndex($"{AppSettings.ElasticsearchPrefix}index_meta", c => c
                .Settings(s => s.NumberOfShards(1))
                .Mappings(ms => ms.Map<IndexMeta>(m => m.AutoMap()))
                .WaitForActiveShards("1")
            );
        }

        public static void MarkAsReady(string index)
        {
            client.Update<IndexMeta, object>(index, d => d.Doc(new { Version = AppSettings.Version }));
        }

        public static void Refresh()
        {
            client.Refresh($"{AppSettings.ElasticsearchPrefix}index_meta");
        }

        public static void UpdateAsync(IndexMeta indexMeta)
        {
            client.IndexDocumentAsync(indexMeta);
        }

        public static IndexMeta GetByName(string name)
        {
            var response = client.Search<IndexMeta>(s => s
                .Query(q => q.Ids(d => d.Values(name)))
            );

            return response.Documents.FirstOrDefault();
        }

        public static IEnumerable<IndexMeta> GetByAlias(string name)
        {
            var response = client.Search<IndexMeta>(s => s
                .Query(q => q.Term(d => d.Alias, name))
                .Sort(sort => sort.Descending(p => p.UpdatedAt))
            );

            return response.Documents;
        }

        public static IndexMeta GetByAliasForCurrentVersion(string name)
        {
            var response = client.Search<IndexMeta>(s => s
                .Query(q => q
                    .Term(t => t.Alias, name) && q
                    .Term(t => t.Version, AppSettings.Version)
                )
                .Sort(sort => sort.Descending(p => p.UpdatedAt))
            );

            return response.Documents.FirstOrDefault();
        }
    }
}
