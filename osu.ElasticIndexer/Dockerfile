FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY *.csproj ./

RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:6.0
RUN apt-get update
RUN DEBIAN_FRONTEND=noninteractive apt-get install -y \
  netcat-openbsd

WORKDIR /app
COPY --from=build-env /app/out .
COPY docker docker

ENTRYPOINT ["/app/docker/entrypoint.sh"]
