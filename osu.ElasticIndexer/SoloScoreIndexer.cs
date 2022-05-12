// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using Nest;

namespace osu.ElasticIndexer
{
    public class SoloScoreIndexer : IIndexer
    {
        public event EventHandler<IndexCompletedArgs> IndexCompleted = delegate { };

        // TODO: maybe have a fixed name?
        public string Name { get; set; } = IndexHelper.INDEX_NAME;
        public long? ResumeFrom { get; set; }

        // use shared instance to avoid socket leakage.
        private readonly ElasticClient elasticClient = AppSettings.ELASTIC_CLIENT;

        private BulkIndexingDispatcher<SoloScore>? dispatcher;

        private Metadata? metadata;

        public void Run()
        {
            metadata = IndexHelper.FindOrCreateIndex(Name);
            if (metadata == null)
            {
                Console.WriteLine($"No metadata found for `{Name}` for version {AppSettings.Schema}...");
                return;
            }

            if (AppSettings.IsWatching)
            {
                if (this.metadata.State == "waiting_for_switchover")
                {
                    Console.WriteLine($"Switching `{Name}` to {metadata.RealName}.");
                    // return;
                }
            }

            var indexCompletedArgs = new IndexCompletedArgs
            {
                Alias = Name,
                Index = metadata.RealName,
                StartedAt = DateTime.Now
            };

            dispatcher = new BulkIndexingDispatcher<SoloScore>(metadata.RealName);

            try
            {
                // TODO: dispatcher should be the separate task?
                // or fix 0 length queue buffer on start?

                // read from queue
                var queueTask = Task.Factory.StartNew(() =>
                    {
                        new Processor<SoloScore>(dispatcher).Run();
                    });

                dispatcher.Run();
                queueTask.Wait();

                IndexCompleted(this, indexCompletedArgs);
            }
            catch (AggregateException ae)
            {
                Console.WriteLine(ae);
                ae.Handle(handleAggregateException);
            }

            // Local function exception handler.
            bool handleAggregateException(Exception ex)
            {
                if (!(ex is InvalidOperationException)) return false;

                Console.Error.WriteLine(ex.Message);
                if (ex.InnerException != null)
                    Console.Error.WriteLine(ex.InnerException.Message);

                return true;
            }
        }

        /// <summary>
        /// Checks if the indexer should wait for the next pass or continue.
        /// </summary>
        /// <returns>true if ready; false, otherwise.</returns>
        private bool checkIfReady()
        {
            if (AppSettings.IsRebuild || IndexHelper.GetIndicesForCurrentVersion(Name).Count > 0)
                return true;

            Console.WriteLine($"`{Name}` for version {AppSettings.Schema} is not ready...");
            return false;
        }
    }
}
