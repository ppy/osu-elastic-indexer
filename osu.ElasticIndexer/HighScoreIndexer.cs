// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-elastic-indexer/master/LICENCE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest;

namespace osu.ElasticIndexer
{
    public class HighScoreIndexer<T> : IIndexer where T : Model
    {
        public event EventHandler<IndexCompletedArgs> IndexCompleted;

        public string Name { get; set; }
        public long? ResumeFrom { get; set; }
        public string Suffix { get; set; }

        private static readonly ElasticClient elasticClient = new ElasticClient
        (
            new ConnectionSettings(new Uri(AppSettings.ElasticsearchHost))
        );

        private BulkIndexingDispatcher<T> dispatcher;

        public void Run()
        {
            var (alias, index) = findOrCreateIndex(Name);
            // find out if we should be resuming
            var resumeFrom = ResumeFrom ?? IndexMeta.GetByName(index)?.LastId;

            Console.WriteLine();
            Console.WriteLine($"{typeof(T)}, index `{index}`, chunkSize `{AppSettings.ChunkSize}`, resume `{resumeFrom}`");
            Console.WriteLine();

            var indexCompletedArgs = new IndexCompletedArgs
            {
                Alias = Name,
                Index = index,
                StartedAt = DateTime.Now
            };

            dispatcher = new BulkIndexingDispatcher<T>(
                alias: Name,
                index: index
            );

            using (var dispatcherTask = dispatcher.Start())
            using (var readerTask = databaseReaderTask(resumeFrom))
            {
                readerTask.Wait();
                dispatcher.prepareToShutdown();
                dispatcherTask.Wait();

                indexCompletedArgs.Count = readerTask.Result;
                indexCompletedArgs.CompletedAt = DateTime.Now;
            }

            updateAlias(Name, index);
            IndexCompleted(this, indexCompletedArgs);
        }

        /// <summary>
        /// Self contained database reader task. Reads the database by cursoring through records
        /// and adding chunks into readBuffer.
        /// </summary>
        /// <param name="resumeFrom">The cursor value to resume from;
        /// use null to resume from the last known value.</param>
        /// <returns>The database reader task.</returns>
        private Task<long> databaseReaderTask(long? resumeFrom)
        {
            return Task.Factory.StartNew(() =>
            {
                long count = 0;

                while (true)
                {
                    try
                    {
                        var chunks = Model.Chunk<T>(AppSettings.ChunkSize, resumeFrom);
                        foreach (var chunk in chunks)
                        {
                            dispatcher.Enqueue(chunk);
                            count += chunk.Count;
                            // update resumeFrom in this scope to allow resuming from connection errors.
                            resumeFrom = chunk.Last().CursorValue;
                        }

                        break;
                    }
                    catch (DbException ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                        Task.Delay(1000).Wait();
                    }
                }

                dispatcher.EnqueueEnd();
                Console.WriteLine($"Finished reading database {count} records.");

                return count;
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
        }

        /// <summary>
        /// Attemps to find the matching index or creates a new one.
        /// </summary>
        /// <param name="name">Name of the alias to find the matching index for.</param>
        /// <returns>Name of index found or created and any existing alias.</returns>
        private (string alias, string index) findOrCreateIndex(string name)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Find or create index for `{name}`...");
            var metas = IndexMeta.GetByAlias(name).ToList();
            var aliasedIndices = elasticClient.GetIndicesPointingToAlias(name);
            string index;

            if (!AppSettings.IsNew)
            {
                // TODO: query ES that the index actually exists.

                index = metas.FirstOrDefault(m => aliasedIndices.Contains(m.Index))?.Index;
                // 3 cases are handled:
                // 1. Index was already aliased and has tracking information; likely resuming from a completed job.
                if (index != null)
                {
                    Console.WriteLine($"Found matching aliased index `{index}`.");
                    return (name, index);
                }

                // 2. Index has not been aliased and has tracking information; likely resuming from an imcomplete job.
                index = metas.FirstOrDefault()?.Index;
                if (index != null)
                {
                    Console.WriteLine($"Found previous index `{index}`.");
                    return (null, index);
                }
            }

            // 3. Not aliased and no tracking information; likely starting from scratch
            var suffix = Suffix ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            index = $"{name}_{suffix}";

            Console.WriteLine($"Creating `{index}` for `{name}`.");
            // create by supplying the json file instead of the attributed class because we're not
            // mapping every field but still want everything for _source.
            var json = File.ReadAllText(Path.GetFullPath("schemas/high_scores.json"));
            elasticClient.LowLevel.IndicesCreate<DynamicResponse>(index, json);

            return (null, index);

            // TODO: cases not covered should throw an Exception (aliased but not tracked, etc).
        }

        private void updateAlias(string alias, string index, bool close = true)
        {
            Console.WriteLine($"Updating `{alias}` alias to `{index}`...");

            var aliasDescriptor = new BulkAliasDescriptor();
            var oldIndices = elasticClient.GetIndicesPointingToAlias(alias);

            foreach (var oldIndex in oldIndices)
                aliasDescriptor.Remove(d => d.Alias(alias).Index(oldIndex));

            aliasDescriptor.Add(d => d.Alias(alias).Index(index));

            Console.WriteLine(elasticClient.Alias(aliasDescriptor));

            // cleanup
            if (!close) return;
            foreach (var toClose in oldIndices.Where(x => x != index))
            {
                Console.WriteLine($"Closing {toClose}");
                elasticClient.CloseIndex(toClose);
            }
        }
    }
}
