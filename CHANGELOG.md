# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Bilingual project roadmap ([ROADMAP.md](ROADMAP.md) / [ROADMAP.he.md](ROADMAP.he.md))
  describing the phased path to v1.2/v1.3, with priorities and acceptance criteria.

### Fixed
- The documented last-resort date fallback (file modified time) now actually runs: the
  ZIP entry's last-write timestamp is captured during indexing and used to date files
  that have no sidecar, no filename date, and no folder year (`DateSource=FileModified`).
  Previously such files landed in `Undated` even when the archive carried a usable
  timestamp.
- **The App download from v1.1.0 does not launch — do not use it.** The single-file EXE
  build crashed on startup for every user (`STATUS_STOWED_EXCEPTION` in
  `Microsoft.UI.Xaml.dll`), caused by a known, still-open upstream WinUI 3 issue combining
  `PublishSingleFile` with an unpackaged, self-contained app
  (see [microsoft/WindowsAppSDK#2597](https://github.com/microsoft/WindowsAppSDK/issues/2597)).
  The App now ships as a self-contained folder (zipped) instead. The CLI was never affected
  (no XAML/WindowsAppSDK dependency) and remains a single `.exe`.

### Changed
- Release pipeline can now also be run manually (`workflow_dispatch`) for testing without
  pushing a tag.
- README/README.he clarify which downloaded file is the App and which is the CLI, and show
  how to run the downloaded CLI binary directly (not just via `dotnet run`).

## [1.1.0] - 2026-07-02

### Changed
- Releases now ship a single self-contained `.exe` per target (App and CLI) instead of
  zips — download one file and run it, no extraction. Builds target win-x64 only, so the
  GitHub Release attaches exactly two `.exe` files — one App, one CLI.
  **The App exe in this release does not launch — see the Fixed entry above.**
- ExifTool is no longer bundled in the download. The App installs it on first run with one
  click into `%LocalAppData%\GPhotosTakeout\Tools`; the CLI reuses that install or accepts
  `--exiftool <path>`. This keeps the install stable across the single-file app's temp
  self-extraction.

## [1.0.0] - 2026-06-23

### Added
- Core engine: metadata re-merge into EXIF/XMP via ExifTool (`-stay_open` batch mode + pool).
- Prefix-based sidecar matching resilient to Google's truncated
  `*.supplemental-metadata.json` names (Issue #353), `-edited`/`(N)` variants, and
  Live/Motion Photo siblings.
- Date hierarchy (JSON → EXIF → filename → folder → mtime) and GPS→IANA timezone resolution
  with correct `OffsetTimeOriginal` / `QuickTime:CreateDate`.
- Atomic, race-free content-hash deduplication; album linking (symlink → hardlink → copy).
- Long-path (`\\?\`) support, multi-archive merge, resumable runs, dry-run preview.
- WinUI3 wizard UI (Hebrew RTL + English LTR, switchable live) and a headless CLI (`gptakeout`).
- Structured logging and per-run log files.
- 87 tests covering matching, dates, dedup, pipeline, concurrency, validation, dry-run,
  ExifTool resilience, long-path, archives, timezone, and albums.

[Unreleased]: https://github.com/itielbru/gphotos-takeout-organizer/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.1.0
[1.0.0]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.0.0
