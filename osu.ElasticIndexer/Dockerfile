FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY *.csproj ./

RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:6.0

WORKDIR /app
COPY --from=build-env /app/out .
COPY docker docker

# see https://github.com/dotnet/runtime/issues/66707 for issue handling signals on arm64
ENTRYPOINT ["/app/docker/wait_term.sh", "osu.ElasticIndexer.dll"]
