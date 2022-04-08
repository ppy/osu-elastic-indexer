# tl;dr

Index all `solo_scores` with corresponding `solo_scores_performance` entry

    docker build -t ${tagname} -f osu.ElasticIndexer/Dockerfile osu.ElasticIndexer

    docker run -e schema=1 -e rebuild=1 -e "elasticsearch__host=http://host.docker.internal:9200" -e "ConnectionStrings__osu=Server=host.docker.internal;Database=osu;Uid=osuweb;SslMode=None;" ${tagname}


Index will be alias at `elasticsearch:9200/solo_scores`

Delete indices:

    http delete :9200/index_meta
    http delete :9200/solo_scores_${timestamp}

or set `action.destructive_requires_name` to `false` on elasticsearch and use

    http delete :9200/solo_scores_*

for fun times >_>


# ElasticIndex

Component for loading [osu!](https://osu.ppy.sh) data into Elasticsearch.

Currently limited to user high scores.

# Requirements

- .NET 6

# Operating modes

The indexer has 2 operating modes,
- building an index from scratch.
- processing from a queue.

## Queue processing

In this mode the indexer will continuously wait for new scores to be indexed on a queue.

## (Re)Populating an index

This mode is used to perform a once-off population or rebuild of the scores index.


## Scenarios

TODO
- create new index

      schema=1 rebuild=1 dotnet run

- continuously process new items
- switching to a new index
- schema updates


# Configuration

The project reads configuration in the following order:
- `appsettings.json`
- `appsettings.{env}.json`
- Environment

where `{env}` is specified by the `APP_ENV` environment variable and defaults to `development`.

# Available settings

Settings should be set in `appsettings` or environment appropriate to the platform, e.g.

`appsettings.json`
```json
{
  "elasticsearch": {
    "host": "http://localhost:9200"
  }
}
```

`Linux / MacOS`
```sh
# note the double underscore
elasticsearch__host=http://localhost:9200 dotnet run
```

---

### `buffer_size`
Number of chunks from the database to read-ahead and buffer.
Defaults to `5`

### `concurrency`
Don't change this.
Defaults to `4`

### `ConnectionStrings:osu`
Standard .NET Connection String to the database.

### `elasticsearch:host`
Elasticsearch host.

### `elasticsearch:prefix`
Assigns a prefix to the indices used.

### `new`
Forces the indexer to always create a new index.

`new` and `resume_from` incompatible and should not be used together.

### `chunk_size`
Batch size when querying from the database.
Defaults to `10000`

### `resume_from`
Cursor value of where to resume reading from.

### `watch`
Sets the program into watch mode.
In watch mode, the program will keep polling for updates to index.

### `polling_interval`
The time between watch polls.

# Index aliasing and resume support
Index aliases are used to support zero-downtime index switches.
When creating new indices, the new index is suffixed with a timestamp.
At the end of the indexing process, the alias is updated to point to the new index and the old index is closed.

The program keeps track of progress information by writing to a `index_meta` index. When starting, it will try to resume from the last known position.

Setting `resume_from=0` will force the indexer to being reading from the beginning.

`new` and `resume_from` incompatible and should not be used together.

# Creating a new index while the watcher is running
The indexer supports running in watch mode while a different indexer process is creating a new index. Once the new index is complete, the watching indexer will automatically switch to updating the new index.

# TODO

- Option to cleanup closed indices.
