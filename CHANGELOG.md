# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-06-25

### Changed
- Releases now ship a single self-contained `.exe` per target (App and CLI, x64 + ARM64)
  instead of zips — download one file and run it, no extraction.
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
