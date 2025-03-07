FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MongoTracker.Sample/MongoTracker.Sample.csproj", "MongoTracker.Sample/"]
COPY ["MongoTracker/MongoTracker.csproj", "MongoTracker/"]
RUN dotnet restore "MongoTracker.Sample/MongoTracker.Sample.csproj"
COPY . .
WORKDIR "/src/MongoTracker.Sample"
RUN dotnet build "MongoTracker.Sample.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MongoTracker.Sample.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MongoTracker.Sample.dll"]
