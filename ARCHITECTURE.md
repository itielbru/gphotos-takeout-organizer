# Architecture

This document explains how the engine is built and *why* it looks the way it does. For the
day-to-day development journal (in Hebrew), see [DEVELOPMENT.md](DEVELOPMENT.md).

## Overview

Clean separation between **Core** (UI-free logic, unit-tested) and **App** (WinUI3) / **CLI**.

```
GPhotosTakeout.sln
├─ src/GPhotosTakeout.Core/    .NET 9 class library — all processing logic
├─ src/GPhotosTakeout.App/     WinUI3 desktop app (unpackaged, Hebrew/English)
├─ src/GPhotosTakeout.Cli/     headless runner (gptakeout)
└─ tests/GPhotosTakeout.Tests/ xUnit — 87 tests
```

Core depends only on `GeoTimeZone` (GPS → IANA) and `Microsoft.Extensions.Logging.Abstractions`.
Dependencies flow one way: the pipeline orchestrates the lower-level modules; modules are
single-purpose and mostly pure.

## Core components

| File | Responsibility |
|------|----------------|
| `Matching/FilenameNormalizer.cs` | Recovers the media name from a JSON sidecar (Issue #353); normalizes `-edited`/`(N)` by prefix |
| `Matching/SidecarMatcher.cs` | Global cross-folder index; sibling inheritance for Live/Motion Photos |
| `Models/TakeoutJson.cs` | JSON model: `photoTakenTime`, `geoData`, `description`, `favorited`; `CapturedUtc`, `BestGeo` |
| `Models/ProcessingOptions.cs`, `OptionsValidator.cs` | Options (structure, albums, duplicates, timezone) + up-front validation |
| `Dates/DateResolver.cs` | Date hierarchy: JSON → EXIF → filename pattern (IMG_/PXL_/Screenshot/WhatsApp) → folder → mtime |
| `Dates/TimezoneResolver.cs` | GPS → IANA (GeoTimeZone), local time + offset, fallback to a home timezone |
| `Metadata/ExifToolBatchWriter.cs` | ExifTool `-stay_open`, `{ready}` protocol, per-format tags, UTF-8/LargeFileSupport flags |
| `Metadata/ExifToolPool.cs` | Bounded pool of ExifTool processes; replaces dead processes; surfaces stderr |
| `Dedup/HashDeduplicator.cs` | Content-hash dedup, **atomic and race-free** (see below) |
| `Albums/AlbumLinker.cs` | symlink → hardlink → copy fallback, with reporting |
| `IO/LongPath.cs` | Long-path support (`\\?\`) with manual `Create`/`OpenRead`/`Move` |
| `Archives/TakeoutArchiveReader.cs` | ZIP indexing + streaming, multi-part merge, **per-archive locking**, hash-on-extract |
| `Pipeline/OutputPathBuilder.cs` | Output path (year/month / albums / flat) + special-folder detection (Archive/Trash/Locked) |
| `Pipeline/ResumeJournal.cs` | Append-only journal for resuming an interrupted run (locked for thread-safety) |
| `Pipeline/ProcessingProgress.cs` | Progress snapshot → `ItemsPerSecond` / `EtaSeconds` |
| `Pipeline/ProcessingPipeline.cs` | Orchestration: index → match → resolve → extract → dedup → metadata → link; progress + cancel |

## The hardest problem — Issue #353

In 2024 Google changed sidecar JSON names from `IMG.jpg.json` to
`IMG.jpg.supplemental-metadata.json`, and **sometimes truncates the name**
(`.supplemental-metad.json`, `.supplem.json`, down to `.s.json`). Sources disagree on the
exact truncation length (46 vs 47 chars), so the matcher **never encodes a magic number** —
it matches by **prefix**. This is resilient to every Google variant observed.

Other edge cases handled: inconsistent extensions (the JSON sometimes includes the media
extension, sometimes not); `-edited` and `(N)` variants in any order (normalized in a loop
until stable); Live/Motion Photos where only the image has a JSON and the video sibling
inherits it; and media↔JSON pairs split across multiple ZIPs.

## Date & timezone hierarchy

`DateResolver` resolves the capture date in priority order: **JSON `photoTakenTime` → existing
EXIF → filename pattern → folder name → file mtime**. `photoTakenTime` is UTC.
`TimezoneResolver` maps the photo's GPS coordinates to an IANA zone (GeoTimeZone), producing
the correct local time plus `OffsetTimeOriginal` for images; videos are written with
`QuickTime:CreateDate` in UTC (`QuickTimeUTC=1`). When there's no GPS, a configurable home
timezone is the fallback.

## Concurrency model

Processing 10K–100K+ files requires parallelism, which opened several traps — all addressed:

- **`ZipArchive` is not thread-safe** → `TakeoutArchiveReader` holds a `SemaphoreSlim` per
  archive: different archives are read in parallel, reads within one archive are serialized.
- **Atomic dedup** → `HashDeduplicator` uses a `ConcurrentDictionary` with claim-based
  ownership (`TryClaim` / `PublishOwnerPath` / `FailOwner`): exactly one thread "wins" each
  hash; duplicates wait for the canonical path. No race, no lost files — and if the owner
  fails, a duplicate keeps its copy instead of the only file being deleted.
- **Hash-on-extract** → SHA-256 is computed during the copy out of the ZIP, so dedup needs no
  extra disk read.
- **Locked resume journal** → reads and writes to the `HashSet` happen under one gate
  (`Contains` racing `Add` is undefined behavior).
- **Split parallelism** → CPU-bound work (matching/hashing) runs at full parallelism; ExifTool
  runs in a small I/O-bound pool to avoid thrashing.
- **ExifTool errors are surfaced** → `ExifToolPool.DrainErrors()` is collected into the
  `ProcessingReport`, so write failures are visible rather than silent.

## Deployment

The app ships **unpackaged / self-contained** (`WindowsPackageType=None` +
`WindowsAppSDKSelfContained`) so it runs full-trust — required to launch `exiftool.exe` and
access the file system. An MSIX/Store build remains available opt-in (`-p:Packaging=true`),
with `runFullTrust` so the packaged app also runs outside the AppContainer.

## Graceful degradation

If ExifTool can't be started (missing or bad path), the run does **not** crash — it degrades
to organizing and dating files without writing metadata (`StartExifPoolOrNull` in Core; path
validation in the CLI).
