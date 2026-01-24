FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Incendia.MongoTracker.Sample/Incendia.MongoTracker.Sample.csproj", "Incendia.MongoTracker.Sample/"]
COPY ["Incendia.MongoTracker/Incendia.MongoTracker.csproj", "Incendia.MongoTracker/"]
RUN dotnet restore "Incendia.MongoTracker.Sample/Incendia.MongoTracker.Sample.csproj"
COPY . .
WORKDIR "/src/Incendia.MongoTracker.Sample"
RUN dotnet build "./Incendia.MongoTracker.Sample.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Incendia.MongoTracker.Sample.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Incendia.MongoTracker.Sample.dll"]
