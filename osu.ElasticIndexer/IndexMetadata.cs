// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Nest;

namespace osu.ElasticIndexer
{
    public class IndexMetadata
    {
        public readonly string Schema;

        public readonly string Name;

        public IndexMetadata(string indexName, string schema)
        {
            Name = indexName;
            Schema = schema;
        }

        public IndexMetadata(IndexName indexName, IndexState indexState)
        {
            Name = indexName.Name;
            Schema = (string)indexState.Mappings.Meta["schema"];
        }

        public void Save(ElasticClient elasticClient)
        {
            elasticClient.Map<SoloScoreIndexer>(mappings => mappings.Meta(
                m => m.Add("schema", Schema)
            ).Index(Name));
        }
    }
}
