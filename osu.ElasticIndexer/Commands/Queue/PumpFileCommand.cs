// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;

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
            if (string.IsNullOrEmpty(AppSettings.Schema))
                throw new MissingSchemaException();

            var processor = new UnrunnableProcessor();

            var value = File.ReadAllText(Filename);
            var redis = new Redis();

            redis.Connection.GetDatabase().ListLeftPush(processor.QueueName, value);

            return 0;
        }
    }
}
