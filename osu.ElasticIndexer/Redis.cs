// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using StackExchange.Redis;

namespace osu.ElasticIndexer
{
    public class Redis
    {
        private readonly string activeSchemasKey = $"osu-queue:score-index:{AppSettings.Prefix}active-schemas";
        private readonly string schemaKey = $"osu-queue:score-index:{AppSettings.Prefix}schema";

        public readonly ConnectionMultiplexer Connection = ConnectionMultiplexer.Connect(AppSettings.RedisHost);

        public bool AddActiveSchema(string value)
        {
            return Connection.GetDatabase().SetAdd(activeSchemasKey, value);
        }

        public void ClearSchemaVersion()
        {
            Connection.GetDatabase().KeyDelete(schemaKey);
        }

        public string[] GetActiveSchemas()
        {
            return Connection.GetDatabase().SetMembers(activeSchemasKey).ToStringArray();
        }

        public string GetSchemaVersion()
        {
            var value = Connection.GetDatabase().StringGet(schemaKey);
            return value.IsNullOrEmpty ? string.Empty : value.ToString();
        }

        public bool RemoveActiveSchema(string value)
        {
            return Connection.GetDatabase().SetRemove(activeSchemasKey, value);
        }

        public void SetSchemaVersion(string value)
        {
            Connection.GetDatabase().StringSet(schemaKey, value);
        }
    }
}
