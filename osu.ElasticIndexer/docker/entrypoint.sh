#!/bin/sh

set -e
set -u

# Wait for dependencies if running queue;
# for other options, just assume they're only run when everything is up.
if [ "$1" = "queue" ]; then
  # TODO: support variables
  echo "Wating for dependencies..."
  /app/docker/wait_for.sh -t 30 elasticsearch:9200
  echo "Elasticsearch is alive."
  /app/docker/wait_for.sh -t 30 db:3306
  echo "Database is alive."
  /app/docker/wait_for.sh redis:6379
  echo "Redis is alive."

  if [ "${START_DELAY:-0}" -gt 0 ]; then
    # Optional delay starting indexer to give time for cluster state to change from red.
    # TODO: poll elasticsearch for cluster status to change from red to yellow/green instead of sleep.
    echo "Wating for $START_DELAY seconds..."
    sleep $START_DELAY
  fi

  echo "Starting indexer."
fi

# see https://github.com/dotnet/runtime/issues/66707 for issue handling signals on arm64
exec /app/docker/wait_term.sh osu.ElasticIndexer.dll "$@"
