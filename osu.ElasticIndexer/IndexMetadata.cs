// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Nest;

namespace osu.ElasticIndexer
{
    public class IndexMetadata
    {
        public readonly string Name;

        public IndexMetadata(IndexName indexName)
        {
            Name = indexName.Name;
        }

        public void Save(ElasticClient elasticClient)
        {
            elasticClient.Map<ScoreIndexer>(mappings => mappings.Index(Name));
        }
    }
}
