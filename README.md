<div align="center">

# Google Photos Takeout Organizer

**Re-merge Google Photos Takeout metadata back into your photos — correct dates, timezones, duplicates, and albums — and get a clean, organized library.**

[English](README.md) · [עברית](README.he.md)

[![CI](https://github.com/itielbru/gphotos-takeout-organizer/actions/workflows/ci.yml/badge.svg)](https://github.com/itielbru/gphotos-takeout-organizer/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/itielbru/gphotos-takeout-organizer/branch/main/graph/badge.svg)](https://codecov.io/gh/itielbru/gphotos-takeout-organizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Latest release](https://img.shields.io/github/v/release/itielbru/gphotos-takeout-organizer?include_prereleases&sort=semver)](https://github.com/itielbru/gphotos-takeout-organizer/releases)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)

<img src="docs/assets/hero.png" alt="Google Photos Takeout Organizer — wizard" width="760">

</div>

## What it does

Google Photos Takeout exports your library as ZIPs where the real metadata lives in
sidecar `.json` files **next to** each photo, not inside it. Most importers ignore those
files, so you end up with wrong dates and lost descriptions. This tool fixes that:

- **Metadata re-merge** — writes the original date, GPS, and description from the Takeout
  JSON back **into** each file's EXIF/XMP (via ExifTool), so any photo app reads them correctly.
- **Timezone-correct dates** — `photoTakenTime` is UTC; the local time and `OffsetTimeOriginal`
  are derived from GPS, and videos get the correct `QuickTime:CreateDate`.
- **Robust matching** — handles Google's truncated `*.supplemental-metadata.json` names
  ([Issue #353](https://github.com/TheLastGimbus/GooglePhotosTakeoutHelper/issues/353)),
  `-edited`/`(N)` variants, and Live/Motion Photo siblings — by prefix, not magic numbers.
- **Atomic deduplication** — content-hash dedup that is race-free under full parallelism.
- **Album organization** — recreates albums via symlink → hardlink → copy fallback.
- **Long-path safe**, multi-archive aware, resumable, with a dry-run preview.
- **Bilingual UI** — Hebrew (RTL) and English (LTR), switchable live.

## Quick start

<div align="center">

[![Download latest release](https://img.shields.io/github/v/release/itielbru/gphotos-takeout-organizer?label=Download%20latest&style=for-the-badge&logo=windows&color=0D7377)](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest)

**Windows 10 / 11 · single .exe · no installer · no zip**

| | Desktop app (wizard) | Command line |
|:--|:--|:--|
| **x64** (Intel/AMD) | [App ⬇](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest) | [CLI ⬇](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest) |
| **ARM64** (Snapdragon) | [App ⬇](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest) | [CLI ⬇](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest) |

</div>

1. Download from the [latest release](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest) — pick the **App** `.exe` for your CPU (x64 / ARM64). It's a single self-contained file — no zip, no install.
2. Run `GPhotosTakeout-App-…-x64.exe`. On first launch, click **Install ExifTool** to enable metadata writing (a one-time ~10 MB download). Everything else works without it.
3. Add your Takeout ZIPs → pick options → run.

> **SmartScreen note:** the executable is not code-signed, so on first run Windows
> SmartScreen may warn you. Click **More info → Run anyway** to continue. This is expected
> for unsigned open-source apps.

### Command line (headless)

```powershell
# Dry-run preview — plans and reports without writing anything
dotnet run --project src/GPhotosTakeout.Cli -- -i takeout-001.zip -o C:\Out --dry-run --report plan.json

# Real run with a CSV report
dotnet run --project src/GPhotosTakeout.Cli -- -i takeout-001.zip -i takeout-002.zip -o C:\Out --report report.csv

# Full help
dotnet run --project src/GPhotosTakeout.Cli -- --help
```

Key flags: `--structure yearmonth|albums|flat`, `--albums`, `--duplicates`,
`--timezone <IANA>`, `--no-metadata`, `--exiftool <path>`, `--dry-run`, `--report <.json|.csv>`,
`--log <path>`, `--no-log`, `-v`. Exit codes: `0` success · `1` completed with errors ·
`2` invalid input · `3` cancelled.

## ExifTool

The app installs a pinned [ExifTool](https://exiftool.org) build on first run with one click
(into `%LocalAppData%\GPhotosTakeout\Tools`), so the download stays a single small `.exe`.
The CLI reuses that install automatically, or you can pass `--exiftool <path>`. When building
from source, download `exiftool.exe` and place it (with its `exiftool_files/` folder) under
`Tools/` next to the executable. Without ExifTool the app still organizes and dates files,
but won't write metadata into them.

## Build from source

```powershell
# Tests (87)
dotnet test tests/GPhotosTakeout.Tests/GPhotosTakeout.Tests.csproj

# App (platform is required — x64 or ARM64)
dotnet build src/GPhotosTakeout.App/GPhotosTakeout.App.csproj -p:Platform=x64
```

Requires the **.NET 9 SDK**. See [CONTRIBUTING.md](CONTRIBUTING.md) for the full dev setup
(including restoring ExifTool locally) and [ARCHITECTURE.md](ARCHITECTURE.md) for the engine
design, concurrency model, and the matching/date/timezone strategy.

## Screenshots

| English (LTR) | עברית (RTL) |
|:---:|:---:|
| ![English wizard](docs/assets/wizard-en.png) | ![Hebrew wizard](docs/assets/wizard-he.png) |

The UI is fully bilingual and switchable live — the whole layout mirrors for Hebrew.

## Issues & support

Bug reports and feature requests are read and taken seriously. If something doesn't work — wrong dates, missing files, a crash — please [open an issue](https://github.com/itielbru/gphotos-takeout-organizer/issues) with as much detail as you can: OS version, which Takeout export caused the problem, and the relevant lines from the log file (`%LocalAppData%\GPhotosTakeout\logs\`). Every report gets a response.

For security issues, please use [private vulnerability reporting](https://github.com/itielbru/gphotos-takeout-organizer/security/advisories/new) instead of a public issue.

## Output structures

```
YearMonth (default)            Albums                   Flat
─────────────────────────      ─────────────────────    ────────────────────
output/                        output/                  output/
└── ALL_PHOTOS/                ├── Summer Trip/         └── ALL_PHOTOS/
    ├── 2023/                  │   └── IMG_001.jpg          ├── IMG_001.jpg
    │   ├── 2023-07/           └── Family/                  └── IMG_002.jpg
    │   │   └── IMG_001.jpg        └── IMG_002.jpg
    │   └── 2023-08/
    │       └── IMG_002.jpg
    └── Undated/
        └── IMG_nodate.jpg
```

Special folders (Archive, Trash, Locked Folder) are always segregated into their own top-level subdirectory, regardless of output structure.

## Album strategy comparison

| Strategy | Disk space | Requires | Photo app support |
|----------|-----------|----------|-------------------|
| Shortcut (symlink → hardlink → copy) | None (unless copy) | Developer Mode for symlinks | Varies |
| Duplicate | 2× | — | Always works |
| JSON Manifest | None | Custom parser | Manual |
| Nothing | None | — | No album grouping |

`Shortcut` is the default: it tries a symlink first, falls back to a hardlink on the same drive, then copies. The fallback chain is fully automatic.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — engine design, concurrency model, the #353 matching trick, date/timezone hierarchy.
- [CONTRIBUTING.md](CONTRIBUTING.md) — build, test, and contribution workflow.
- [SECURITY.md](SECURITY.md) — security policy (no telemetry; processes files locally).
- [docs/troubleshooting.md](docs/troubleshooting.md) — wrong dates, missing files, ExifTool issues, Live Photos, exit codes.
- [docs/cli-cookbook.md](docs/cli-cookbook.md) — CLI recipes: dry-run, automation scripts, report parsing, exit code handling.
- [docs/performance.md](docs/performance.md) — tuning `--cpu`, `--exif-parallel`, and advice for large datasets and NAS targets.
- [README.he.md](README.he.md) — Hebrew documentation.
- [DEVELOPMENT.md](DEVELOPMENT.md) — development journal (Hebrew).

## License

[MIT](LICENSE) © 2026 Itiel Bru.
