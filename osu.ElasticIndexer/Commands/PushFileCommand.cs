// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("push-file", Description = "Push contents of a file to the queue for testing.")]
    public class PushFileCommand : ProcessorCommandBase
    {
        [Argument(0)]
        [Required]
        public string Filename { get; set; } = string.Empty;

        public int OnExecute(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();

            var value = File.ReadAllText(Filename);

            var redis = new Redis();
            var database = redis.Connection.GetDatabase();
            var queueName = $"osu-queue:score-index-{AppSettings.Schema}";

            database.ListLeftPush(queueName, value);

            return 0;
        }
    }
}
