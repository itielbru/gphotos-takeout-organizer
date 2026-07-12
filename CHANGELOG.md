# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2026-07-12

### Added
- **ExifTool now ships inside the installer and the portable zip** — metadata
  writing works out of the box, with no separate first-run download step. The
  release pipeline downloads the pinned build (version + SHA-256 taken from
  `ExifToolInstaller.cs`, still the single source of truth), verifies the
  checksum, and stages it in the App's `Tools\` folder, which the App and the
  CLI already probe. The in-app one-click installer remains for source builds
  and for updating ExifTool later.
- The run report now counts skipped non-media files (`SkippedNonMedia`), and the
  run log records the selected input paths — previously a run's log did not say
  what input it processed, which made misconfigured runs hard to diagnose.

### Fixed
- The pipeline no longer treats every input file as a photo. Only known
  photo/video extensions (including camera raw and motion-photo companions) are
  organized and sent to ExifTool; documents, executables, and other stray files
  are skipped and counted. Previously, pointing the app at a folder containing
  arbitrary files organized DLLs into the photo library by their file dates and
  flooded the run with hundreds of ExifTool "writing not supported" errors.

## [1.2.2] - 2026-07-11

### Fixed
- **The shipped App now actually launches.** Every released App build (portable zip
  since v1.1.0 and the v1.2.1 installer) crashed immediately on startup with
  `0xC000027B` (STATUS_STOWED_EXCEPTION) in `Microsoft.UI.Xaml.dll`, because
  `EnableMsixTooling=false` on unpackaged builds prevented `dotnet publish` from
  generating `resources.pri` (the compiled XAML). Dev builds were unaffected, which
  is why the bug shipped repeatedly. The earlier diagnosis blaming WinUI 3's
  single-file mode (v1.1.1 notes) was incomplete — the missing PRI was the real
  cause all along. Verified on a real Windows 11 machine: pre-fix publish crashes,
  post-fix publish launches.
- The Release workflow no longer runs twice per release: the `push: tags` trigger
  is removed (publishing the draft release creates the tag, which re-triggered a
  full rebuild and re-ran the release step against the already-published release).
  Releases are dispatched manually from the Actions tab.

### Changed
- The installer filename is now version-stable (`GPhotosTakeout-Setup-win-x64.exe`,
  version in the EXE metadata and release tag) so the README's download button can
  link directly to `releases/latest/download/…` and always fetch the newest release.
  Both READMEs now lead with that one-click direct-download button.

## [1.2.1] - 2026-07-11

### Added
- One-click Windows installer (`GPhotosTakeout-Setup-<version>-win-x64.exe`), built
  with Inno Setup by the release pipeline. Installs the App and the CLI per-user
  (no admin rights needed), creates a Start Menu shortcut and an optional desktop
  icon, and registers a standard uninstaller. The portable zip and the standalone
  CLI exe are still published alongside it for users who prefer no install.

## [1.2.0] - 2026-07-10

### Added
- Bilingual project roadmap ([ROADMAP.md](ROADMAP.md) / [ROADMAP.he.md](ROADMAP.he.md))
  describing the phased path to v1.2/v1.3, with priorities and acceptance criteria.

### Added
- New EXIF date-fallback tier: when the sidecar has no usable date, the capture date
  embedded in the file itself (EXIF `DateTimeOriginal` for photos, the QuickTime
  creation time for videos) is read after extraction — via the managed
  MetadataExtractor library, so it works without ExifTool — and outranks the weaker
  filename/folder/modified-time tiers. Opt out with `--no-exif-fallback`. Note:
  dry-run never extracts, so it may report a weaker `DateSource` than the real run.
- The `duplicate` album strategy now works: identical album copies are placed as
  physical files under `Albums/<name>/` instead of silently behaving like `nothing`.
- The `json` album strategy now works end-to-end: `--albums json` parses (it previously
  exited with a usage error) and the run writes an `albums.json` manifest at the output
  root mapping each album to its files (paths relative to the output root, forward
  slashes). Resumed runs merge into the existing manifest instead of overwriting it.
- Album membership is now also materialized under the `flat` output structure and with
  `--duplicates keepall` (previously only `yearmonth` + `keepbest`).

### Fixed
- Album entries are now created regardless of which copy wins the de-duplication race.
  Previously the `Albums/` link was created only when the album copy lost the race, so
  archives where the album copy appeared first produced no album entry at all.
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
- The default fallback timezone is now the machine's own timezone (as an IANA id)
  instead of a hardcoded `Asia/Jerusalem`. An explicit `--timezone` / app setting still
  wins.
- Temp-file cleanup failures are now logged as warnings instead of being silently
  swallowed.
- `Package.appxmanifest` is bumped to 1.1.0.0 and the MSIX workflow now rewrites the
  manifest version from `Directory.Build.props`, so the two can no longer drift.
- The CLI argument parser is now covered by unit tests (~30 cases: defaults, every
  flag, enum aliases, and error paths), and the CI line-coverage gate was raised from
  60% to 65%.
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

[Unreleased]: https://github.com/itielbru/gphotos-takeout-organizer/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.3.0
[1.2.2]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.2.2
[1.2.1]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.2.1
[1.2.0]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.2.0
[1.1.0]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.1.0
[1.0.0]: https://github.com/itielbru/gphotos-takeout-organizer/releases/tag/v1.0.0
