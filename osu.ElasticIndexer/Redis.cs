// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using StackExchange.Redis;

namespace osu.ElasticIndexer
{
    public class Redis
    {
        private readonly string key = $"osu-queue:score-index:{AppSettings.Prefix}schema";

        public readonly ConnectionMultiplexer Connection = ConnectionMultiplexer.Connect(AppSettings.RedisHost);

        public void ClearSchemaVersion()
        {
            Connection.GetDatabase().KeyDelete(key);
        }

        public string GetSchemaVersion()
        {
            var value = Connection.GetDatabase().StringGet(key);
            return value.IsNullOrEmpty ? string.Empty : value.ToString();
        }

        public void SetSchemaVersion(string value)
        {
            Connection.GetDatabase().StringSet(key, value);
        }
    }
}
