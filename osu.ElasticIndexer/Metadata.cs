// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Nest;

namespace osu.ElasticIndexer
{
    public class Metadata
    {
        public bool IsAliased { get; set; }

        public long LastId { get; set; }
        public string RealName { get; set; }
        public long? ResetQueueTo { get; set; }
        public string Schema { get; set; } = "";
        public string State { get; set; } = "";
        public DateTimeOffset? UpdatedAt { get; set; }

        public Metadata(string indexName, string schema)
        {
            RealName = indexName;
            Schema = schema;
        }

        public Metadata(IndexName indexName, IndexState indexState)
        {
            RealName = indexName.Name;

            this.UpdateWith(indexState);
        }

        public void Save()
        {
            AppSettings.ELASTIC_CLIENT.Map<SoloScoreIndexer>(mappings => mappings.Meta(
                m => m
                    .Add("last_id", LastId)
                    .Add("reset_queue_to", ResetQueueTo)
                    .Add("schema", Schema)
                    .Add("state", State)
                    .Add("updated_at", DateTimeOffset.UtcNow)
            ).Index(RealName));
        }

        // TODO: should probably create whole object
        private void UpdateWith(IndexState indexState)
        {
            var meta = indexState.Mappings.Meta;
            LastId = Convert.ToInt64(meta["last_id"]);
            ResetQueueTo = meta.ContainsKey("reset_queue_to") ? Convert.ToInt64(meta["reset_queue_to"]) : null;
            Schema = (string)meta["schema"];
            State = (string)meta["state"];
            UpdatedAt = meta.ContainsKey("updated_at") ? DateTimeOffset.Parse((string)meta["updated_at"]) : null;
        }
    }
}
