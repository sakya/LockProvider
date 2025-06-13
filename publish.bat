rd /s /q .\dist\win-x64

dotnet clean LockProvider.sln -c Release
dotnet publish LockProviderApi\LockProviderApi.csproj -c Release --runtime win-x64 -p:PublishReadyToRun=true --self-contained --output .\dist\win-x64
