// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Nest;

namespace osu.ElasticIndexer
{
    public class Metadata
    {
        public readonly string RealName;
        public readonly string Schema;

        public Metadata(string indexName, string schema)
        {
            RealName = indexName;
            Schema = schema;
        }

        public Metadata(IndexName indexName, IndexState indexState)
        {
            RealName = indexName.Name;
            Schema = (string)indexState.Mappings.Meta["schema"];
        }

        public void Save(ElasticClient elasticClient)
        {
            elasticClient.Map<SoloScoreIndexer>(mappings => mappings.Meta(
                m => m.Add("schema", Schema)
            ).Index(RealName));
        }
    }
}
