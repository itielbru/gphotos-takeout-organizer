# Contributing

Thanks for your interest in improving Google Photos Takeout Organizer!

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10/11 (the App project is WinUI3; the Core and CLI are cross-platform .NET)
- **ExifTool** (only needed to package/run with metadata writing) — see below.

## Build & test

```powershell
# Run the test suite (87 tests)
dotnet test tests/GPhotosTakeout.Tests/GPhotosTakeout.Tests.csproj

# Build the Core and CLI
dotnet build src/GPhotosTakeout.Core/GPhotosTakeout.Core.csproj -c Release
dotnet build src/GPhotosTakeout.Cli/GPhotosTakeout.Cli.csproj  -c Release

# Build the WinUI app — a platform is REQUIRED (x64 or ARM64)
dotnet build src/GPhotosTakeout.App/GPhotosTakeout.App.csproj -p:Platform=x64
```

`Core`, `Cli`, and `Tests` build clean; the App must be built with an explicit `-p:Platform`.

## Restoring ExifTool locally

ExifTool is ~35 MB and is **not** committed (it's git-ignored). To build a packaged/portable
output with metadata writing:

1. Download the Windows build from <https://exiftool.org>.
2. Extract it and rename `exiftool(-k).exe` → `exiftool.exe`.
3. Place `exiftool.exe` **and its `exiftool_files/` folder** into
   `src/GPhotosTakeout.App/Tools/`.

The app locates it automatically via `Services/ExifToolLocator.cs`. Without it, the app still
runs but skips metadata writing.

## Code style

`.editorconfig` is enforced at build time (`EnforceCodeStyleInBuild`), and Core/CLI/Tests
build under analyzers. Please run `dotnet format` (or let your IDE apply the `.editorconfig`)
before committing.

## Pull requests

- Keep PRs focused; one logical change per PR.
- Add or update tests for behavior changes — the Core suite must stay green.
- Make sure `dotnet test` passes and the App builds for `x64` (and `ARM64` if you touched
  platform-specific code).
- Describe *why*, not just *what*, in the PR description.

## Reporting bugs / requesting features

Use the [issue templates](.github/ISSUE_TEMPLATE). For security issues, see [SECURITY.md](SECURITY.md).
