# ElasticIndexer

Component for loading [osu!](https://osu.ppy.sh) scores into Elasticsearch.

## Requirements

- .NET 6
- Elasticsearch 7
- Redis 6

## Elasticsearch 8 compatiblity

If using Elasticsearch 8, a minimum version of Elasticsearch 8.2 is required.

The following env needs to be set on the indexer:

```bash
ELASTIC_CLIENT_APIVERSIONING=true
```

and the following must be set in elasticsearch server configuration
`elasticsearch.yml`

```yml
xpack.security.enabled: false
```

or docker environment, e.g. in docker compose:
```yml
environment:
  xpack.security.enabled: false
```

This will enable http connections to elasticsearch and disable the https and authentication requirement, as well as, returning a compatible response to the client.


# Usage

## Schema

A string value is used to indicate the current schema version to be used.

When the queue processor is running, it will store the version it is processing in a set in Redis at `osu-queue:score-index:${prefix}active-schemas`.

If a queue processor is stops automatically due to a schema version change,
it will remove the version it is processing from the set of versions; it _will not_ be removed if the processor if stopped manually or from processor failures; this is to allow other services to continue pushing to those queues.

## Adding items to be indexed

Scores with `preserve`=`true` belonging to a user with `user_warnings`=`0` will be added to the index,
scores where any of the previous conditions are false will be removed from the index.

Push items to `osu-queue:score-index-${schema}`

## Switching to a new schema

Run `dotnet run schema --schema ${schema}` or set `osu-queue:score-index:${prefix}schema` directly in Redis

### Automatic index switching

If there is an already running indexer watching the queue for the new schema version,
it will automatically update the alias to point to the new index.
When the alias is updated, any index previously used by the alias will be closed.

The alias will not be updated if:
- the schema value does not change
- the indexer processing the queue for that version was not running before the change.

When the schema version changes, all indexers processing the queues for any other version will automatically stop.

# Configuration

Configuration is loaded from environment variables. No environment files are automatically loaded.

To read environment variables from an env file, you can prefix the command to run with `env $(cat {envfile})` replacing `{envfile}` with your env file, e.g.
> Note that this method of passing envvars does not support values with spaces.

    env $(cat .env) dotnet run

Additional envs can be set:

    env $(cat .env) SCHEMA=1 dotnet run

## Environment Variables

### BATCH_SIZE
Maximum number of items to handle/dequeue per batch. This affects the size of the `_bulk` request sent to Elasticsearch.

Defaults to `10000`.

### BUFFER_SIZE

Maximum number of `BATCH_SIZE * BUFFER_SIZE` items allowed inflight during queue processing.

Defaults to `5` (default of `50000` items).

### DB_CONNECTION_STRING
Connection string for the database connection.
Standard `MySqlConnector` connection string.

### ES_INDEX_PREFIX
Optional prefix for the index names in elasticsearch.

### ES_HOST
Url to the Elasticsearch host.

Defaults to `http://elasticsearch:9200`

### REDIS_HOST
Redis connection string; see [here](https://stackexchange.github.io/StackExchange.Redis/Configuration.html#configuration-options) for configuration options.

Defaults to `redis`

### SCHEMA
Schema version for the queue; see [Schema](#schema).

# Commands

This documentation assumes `dotnet run` can be used;
in cases where `dotnet run` is not available, the assembly should be used, e.g. `dotnet osu.ElasticIndexer.dll`

## Watching a queue for new scores

Running `queue` will automatically create an index if an open index matching the requested `schema` does not exist.
If a matching open index exists, it will be reused.

    SCHEMA=${schema} dotnet run queue

e.g.

    SCHEMA=1 dotnet run queue

## Getting the current schema version

    dotnet run schema

## Setting the schema version

    dotnet run schema set ${schema}

## Unsetting the schema version

This is used to unset the schema version for testing purposes.

    dotnet run schema clear

## Changing the alias to a new index

The index the alias points to can be changed manually:

    dotnet run alias --schema 1

will update the index alias to the latest index with schema `1` tag.

## List indices

To list all indices and their corresponding states (schema, aliased, open or closed)

    dotnet run list

## Closing unused indices

This will close all score indices except the active one, unloading them from Elasticsearch's memory pool.

    dotnet run close

A specific index can be closed by passing in index's name as an argument; e.g. the following will close `index_1`:

    dotnet run close index_1

## Cleaning up closed indices

This will delete all closed indices and free up the storage space used by those indices.
The command will only delete an index if it is in the `closed` state.

    dotnet run delete

Passing arguments to the command will delete the matching index:

    dotnet run delete index_1

## Adding fake items to the queue

For testing purposes, we can add fake items to the queue:

    SCHEMA=1 dotnet run fake

It should be noted that these items will not exist or match the ones in the database.

## Queuing a specific score for indexing

    SCHEMA=${schema} dotnet run index ${id}

will queue the score with `${id}` for indexing; the score will be added or deleted as necessary, according to the value of `SoloScore.ShouldIndex`.

See [Queuing items for processing from another client](#queuing-items-for-processing-from-another-client)

## Adding existing database records to the queue

    SCHEMA=1 dotnet run all

will read existing `solo_scores` in chunks and add them to the queue for indexing. Only scores with a corresponding `phpbb_users` entry will be queued.

Extra options:

`--from {id}`: `solo_scores.id` to start reading from

`--switch`: Sets the schema version after the last item is queued; it does not wait for the item to be indexed; this option is provided as a conveninence for testing.

## Listing known versions currently being processed

    dotnet run active-schemas

will list the versions known to have queue processors listening on the queue.


## Manually add or remove known versions

For debugging purposes or to perform and manual maintenance or cleanups, the list of versions can be updated manually:

    dotnet run active-schemas add ${schema}
    dotnet run active-schemas remove ${schema}

# (Re)Populating an index

Populating an index is done by pushing score items to a queue.

# Docker

    docker build -t ${tagname} -f osu.ElasticIndexer/Dockerfile osu.ElasticIndexer

    docker run -e SCHEMA=1 -e "ES_HOST=http://host.docker.internal:9200" -e "ES_INDEX_PREFIX=docker." -e "REDIS_HOST=host.docker.internal" -e "DB_CONNECTION_STRING=Server=host.docker.internal;Database=osu;Uid=osuweb;SslMode=None;" ${tagname} ${cmd}

where `${cmd}` is the command to run, e.g. `dotnet osu.ElasticIndexer.dll queue`

# Typical use-cases

## Queuing items for processing from another client

Push items into the Redis queue "`osu-queue:score-index-${schema}`"
e.g.

```csharp
ListLeftPush("osu-queue:score-index-1", "{ \"ScoreId\": 1 }");
```

or from redis-cli:
```
LPUSH "osu-queue:score-index-1" "{\"ScoreId\":1}"
```

### Indexing a score by `id`
```json
{ "ScoreId": 1 }
```

### Queuing a whole score

```json
{
    "Score": {Solo.Score}
}
```
