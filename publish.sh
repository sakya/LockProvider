#!/bin/bash
rm -rf ./dist/linux-x64

dotnet clean LockProvider.sln -c Release
dotnet publish LockProviderApi/LockProviderApi.csproj -c Release --runtime linux-x64 -p:PublishReadyToRun=true --self-contained --output ./dist/linux-x64
