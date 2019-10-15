dotnet publish -r win-x64 -f netcoreapp3.0 -c Release /p:PublishSingleFile=true
dotnet publish -r linux-x64 -f netcoreapp3.0 -c Release /p:PublishSingleFile=true
dotnet publish -r osx-x64 -f netcoreapp3.0 -c Release /p:PublishSingleFile=true