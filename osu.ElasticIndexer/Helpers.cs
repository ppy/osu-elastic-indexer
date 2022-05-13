// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.ElasticIndexer
{
    public class Helpers
    {
        private const string key = "osu-queue:score-index:schema";

        public static string GetSchemaVersion()
        {
            return AppSettings.Redis.GetDatabase().StringGet(key);
        }

        public static void SetSchemaVersion(string value)
        {
            AppSettings.Redis.GetDatabase().StringSet(key, value);
        }
    }
}
