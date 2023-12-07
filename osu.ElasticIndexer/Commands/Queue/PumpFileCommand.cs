// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer.Commands.Queue
{
    [Command("pump-file", Description = "Pump contents of a file to the queue for testing.")]
    public class PumpFileCommand
    {
        [Argument(0)]
        [Required]
        public string Filename { get; set; } = string.Empty;

        public int OnExecute(CancellationToken cancellationToken)
        {
            var processor = new UnrunnableProcessor();

            var value = File.ReadAllText(Filename);
            var redis = RedisAccess.GetConnection();

            redis.GetDatabase().ListLeftPush(processor.QueueName, value);

            return 0;
        }
    }
}
