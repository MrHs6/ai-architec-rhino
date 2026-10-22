# AI Archi-Tec — plugin source

The canonical product overview, graphical workflow and downloads are in the [repository README](../README.md).

- [Step-by-step installation](../docs/INSTALLATION.en.md)
- [MCP client setup and recovery](../docs/MCP-QUICKSTART.en.md)
- [Architecture and source modules](../docs/ARCHITECTURE.en.md)
- [Provider protocols](../docs/PROVIDERS.md)
- [Build/distribution](docs/DISTRIBUTION.md)
- [Validation record](docs/VALIDATION.md)
- [Data and credentials](PRIVACY.md)
- [فارسی](../docs/README.fa.md)

Build on Windows with .NET SDK 8:

```powershell
.\scripts\build.ps1 -UseNuGet
.\scripts\package.ps1
```

The package version is defined in manifest.yml and the Rhino project file. CI builds the pinned official SDK, runs automated checks and packages Yak plus a Windows installer. Licensed Rhino is required for native execution and UI/geometry acceptance. See the repository's actual LICENSE.md for source and binary evaluation terms.