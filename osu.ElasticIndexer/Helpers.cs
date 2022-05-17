// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.ElasticIndexer
{
    public class Helpers
    {
        private const string key = "osu-queue:score-index:schema";

        public static void ClearSchemaVersion()
        {
            AppSettings.Redis.GetDatabase().KeyDelete(key);
        }

        public static string GetSchemaVersion()
        {
            var value = AppSettings.Redis.GetDatabase().StringGet(key);
            return value.IsNullOrEmpty ? string.Empty : value.ToString();
        }

        public static void SetSchemaVersion(string value)
        {
            AppSettings.Redis.GetDatabase().StringSet(key, value);
        }
    }
}
