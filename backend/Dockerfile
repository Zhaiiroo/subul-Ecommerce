# build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish backend.csproj -c Release -o /app/publish --no-restore

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# Local image storage writes to /img (ContentRootPath/../img). It is the only
# path the process writes to, so it is the only one the app user owns; the
# published code stays root-owned and read-only to the running process.
# A bind mount over /img must be owned by the same UID on the host (1654).
RUN mkdir /img && chown "$APP_UID" /img
ENV ASPNETCORE_URLS=http://+:5101
EXPOSE 5101
# APP_UID (1654) is the non-root user the official image ships for this.
USER $APP_UID
ENTRYPOINT ["dotnet", "backend.dll"]
