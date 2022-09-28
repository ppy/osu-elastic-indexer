FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out
# get rid of bloat
RUN rm -rf ./out/runtimes ./out/osu.Game.Resources.dll ./out/osuTK.dll

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build-env /app/out .
COPY docker docker

# see https://github.com/dotnet/runtime/issues/66707 for issue handling signals on arm64
ENTRYPOINT ["/app/docker/wait_term.sh", "osu.ElasticIndexer.dll"]
