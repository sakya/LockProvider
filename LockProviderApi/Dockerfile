FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5000
EXPOSE 5001
EXPOSE 5002

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LockProviderApi/LockProviderApi.csproj", "LockProviderApi/"]
COPY ["LockProvider/LockProvider.csproj", "LockProvider/"]
RUN dotnet restore "LockProviderApi/LockProviderApi.csproj"
COPY . .
WORKDIR "/src/LockProviderApi"
RUN dotnet build "./LockProviderApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LockProviderApi.csproj" -c $BUILD_CONFIGURATION -p:PublishReadyToRun=true -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LockProviderApi.dll"]
