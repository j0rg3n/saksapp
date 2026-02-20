FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SaksAppWeb/SaksAppWeb.csproj", "SaksAppWeb/"]
RUN dotnet restore "SaksAppWeb/SaksAppWeb.csproj"
COPY . .
WORKDIR "/src/SaksAppWeb"
RUN dotnet build "./SaksAppWeb.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SaksAppWeb.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# The /app/db directory is intended to be a volume mount.
# On Linux hosts, the directory must be writable by the non-root user (1654).
# We create it and set permissions in the image, but volume mounts can override this.
USER root
RUN mkdir -p /app/db /app/dp_keys && chown -R $APP_UID:$APP_UID /app/db /app/dp_keys
USER $APP_UID
ENTRYPOINT ["dotnet", "SaksAppWeb.dll"]
