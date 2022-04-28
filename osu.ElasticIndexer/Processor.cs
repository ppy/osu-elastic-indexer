// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Dapper.Contrib.Extensions;
using MySqlConnector;
using Nest;
using Newtonsoft.Json;
using osu.Server.QueueProcessor;

namespace osu.ElasticIndexer
{
    public class Processor<T> : QueueProcessor<ScoreItem> where T : Model
    {
        public static readonly string QueueName = $"score-index-{AppSettings.Schema}";

        internal Processor() : base(new QueueConfiguration { InputQueueName = QueueName })
        {
        }

        internal Processor(BulkIndexingDispatcher<T> dispatcher) : this()
        {

        }

        protected override void ProcessResult(ScoreItem item)
        {
            Console.WriteLine(item);
        }
    }
}
