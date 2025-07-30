# Balrog Launcher

WPF launcher rewritten for .NET 8 using MVVM and dependency injection. Assets are loaded from the `Assets` folder and should be tracked with Git LFS.

## Build

```
dotnet publish BalrogLauncher/BalrogLauncher.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

## Configuration
Insert API endpoints, client executable path and patch URLs where marked with `//TODO:` comments in the source.
