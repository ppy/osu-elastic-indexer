# ElasticIndexer

Component for loading [osu!](https://osu.ppy.sh) scores into Elasticsearch.

## Requirements

- .NET 6
- Elasticsearch 7
- Redis

# Usage

## Schema

A string value is used to indicate the current schema version to be used.

## Adding items to be indexed

Scores with `preserve`=`true` will be added to the index,
scores with `preserve`=`false` will be removed from the index.

Push items to `osu-queue:score-index-${schema}`

## Switching to a new schema

Run `dotnet run schema --schema ${schema}` or set `osu-queue:score-index:schema` directly in Redis

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

    env $(cat .env) dotnet run

additional envs can be set:

    env $(cat .env) schema=1 dotnet run

envvars with spaces are not supported.

# Commands

This documentation assumes `dotnet run` can be used;
in cases where `dotnet run` is not available, the assembly should be used, e.g. `dotnet osu.ElasticIndexer.dll`

## Watching a queue for new scores

    schema=${schema} dotnet run queue

e.g.

    schema=1 dotnet run queue

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

## Cleaning up closed indices

This will delete all closed indices and free up the storage space used by those indices.
TODO: single index option?

    dotnet run cleanup

## Closing unused indices

This will close all score indices except the active one, unloading them from Elasticsearch's memory pool.
TODO: single index option?

    dotnet run cleanup

## Adding fake items to the queue

For testing purposes, we can add fake items to the queue:

    schema=1 dotnet run fake

It should be noted that these items will not exist or match the ones in the database.

# (Re)Populating an index

Populating an index is done by pushing score items to a queue.

# Docker

    docker build -t ${tagname} -f osu.ElasticIndexer/Dockerfile osu.ElasticIndexer

    docker run -e schema=1 -e "ES_HOST=http://host.docker.internal:9200" -e "prefix=docker." -e "REDIS_HOST=host.docker.internal" -e "DB_CONNECTION_STRING=Server=host.docker.internal;Database=osu;Uid=osuweb;SslMode=None;" ${tagname} ${cmd}

where `${cmd}` is the command to run, e.g. `dotnet osu.ElasticIndexer.dll queue`

# Typical use-cases
