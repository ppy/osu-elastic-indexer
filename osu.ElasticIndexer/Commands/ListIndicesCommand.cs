// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Elasticsearch.Net;
using McMaster.Extensions.CommandLineUtils;

namespace osu.ElasticIndexer.Commands
{
    [Command("list", Description = "Lists indices")]
    public class ListIndicesCommand : ProcessorCommandBase
    {
        private readonly Client client = new Client();

        public int OnExecute()
        {
            var indices = client.GetIndices(client.AliasName, ExpandWildcards.All);

            if (indices.Count > 0)
            {
                var response = client.ElasticClient.Cat.Indices(descriptor => descriptor.Index(indices.Keys.Select(k => k.Name).ToArray()));

                foreach (var record in response.Records)
                {
                    var indexState = indices[record.Index];
                    var schema = indexState.Mappings.Meta?["schema"];
                    var aliased = indexState.Aliases.ContainsKey(client.AliasName);

                    Console.WriteLine($"{record.Index} schema:{schema} aliased:{aliased} {record.Status} docs {record.DocsCount} deleted {record.DocsDeleted} {record.PrimaryStoreSize}");
                }
            }

            return 0;
        }
    }
}
