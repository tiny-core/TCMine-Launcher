FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TCMine-Server/TCMine-Server.csproj", "TCMine-Server/"]
RUN dotnet restore "TCMine-Server/TCMine-Server.csproj"
COPY . .
WORKDIR "/src/TCMine-Server"
RUN dotnet build "./TCMine-Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TCMine-Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
# Pasta persistente para a BD SQLite, updates e overrides (volume /data),
# gravável pelo utilizador não-root da imagem.
USER root
RUN mkdir -p /data && chown $APP_UID:0 /data
USER $APP_UID
WORKDIR /app
# Defaults para /data — assim no deploy basta montar /data e definir
# CF_API_KEY + ADMIN_PASSWORD (estas variáveis ficam opcionais).
ENV DB_PATH=/data/tcmine.db \
    UPDATES_DIR=/data/updates \
    OVERRIDES_DIR=/data/overrides
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TCMine-Server.dll"]
