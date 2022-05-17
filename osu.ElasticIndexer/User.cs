// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Dapper;
using Dapper.Contrib.Extensions;
using MySqlConnector;

namespace osu.ElasticIndexer
{
    /// <summary>
    /// User mapping model that contains only the properties needed for the indexer.
    /// </summary>
    [Table("phpbb_users")]
    public class User : Model
    {
        [Computed]
        public override long CursorValue => user_id;

        [Computed]
        public bool ShouldIndex => true;

        [Computed]
        public string country_acronym { get; set; } = "";

        public string country_code => country_acronym;

        public uint user_id { get; set; }

        public override string ToString() => $"user_id: {user_id} country_code: {country_code}";

        public static Dictionary<uint, User> FetchUserMappings(IEnumerable<SoloScore> scores)
        {
            var userIds = scores.Select(s => s.UserId);
            using (var dbConnection = new MySqlConnection(AppSettings.ConnectionString))
            {
                dbConnection.Open();
                return dbConnection
                    .Query<User>($"select user_id, country_acronym from phpbb_users where user_id in @userIds", new { userIds })
                    .ToDictionary(u => u.user_id);
            }
        }
    }
}
