FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/global.json ./backend/
COPY backend/Ffmt.Core/ ./backend/Ffmt.Core/
COPY docker/ws_worker/WsWorker/ ./docker/ws_worker/WsWorker/
WORKDIR /src/docker/ws_worker/WsWorker
RUN dotnet restore && dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "WsWorker.dll"]
