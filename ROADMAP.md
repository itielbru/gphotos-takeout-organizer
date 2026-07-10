# Roadmap

[English](ROADMAP.md) · [עברית](ROADMAP.he.md)

The path from the current release (v1.1.0) to a polished, finished product. Scope
decisions baked into this roadmap: the product stays **Windows-only**, and distribution
stays **GitHub Releases** (no code signing, winget, or Store for now).

Each item carries a priority (**P0** = correctness bug users can hit today,
**P1** = important gap, **P2** = polish) and an acceptance criterion. Checkboxes track
status; items link to the code they touch.

## Phase 1 — Core correctness (target: v1.2.0)

Closing the gap between what the docs promise and what the pipeline actually runs.

- [x] **1.1 Wire the file-modified-time date fallback** (P0)
  `DateResolver` supports a last-resort file-modified-time tier, but the pipeline always
  passes `fileModifiedUtc: null`, so the tier never fires. Capture
  `ZipArchiveEntry.LastWriteTime` during indexing and pass it through.
  *Accept:* a media file with no sidecar, no filename date, and no folder year is dated
  from its ZIP entry timestamp (`DateSource=FileModified`) and lands in the right
  year/month folder; covered by an integration test.

- [x] **1.2 Fix `--albums json` CLI parsing** (P0)
  The help text advertises `--albums json`, but the enum member is `JsonManifest`, so the
  documented value exits with a usage error. Accept `json` as an alias.
  *Accept:* `gptakeout --albums json` parses; unit test covers the alias.

- [x] **1.3 Fix the album-shortcut ordering race** (P0)
  An album link is created only when the album copy *loses* the dedup race. If the album
  copy is extracted first (becomes the hash owner), no `Albums/` entry is ever created.
  Materialize the album entry on every path: duplicate branch, owner branch, and KeepAll.
  *Accept:* with the album entry ordered first in the archive, the `Albums/<name>/<file>`
  entry still exists after the run; test asserts it unconditionally.

- [x] **1.4 Implement the `Duplicate` album strategy** (P1)
  `--albums duplicate` currently behaves like `nothing` — the album copy is deduped away
  and nothing is materialized. It should place a physical copy under `Albums/<name>/`.
  *Accept:* test asserts a real file (not a link) exists in the album folder.

- [x] **1.5 Implement the `JsonManifest` album strategy** (P1)
  Emit an `albums.json` manifest at the output root (schema v1: album name → files with
  paths relative to the output root, forward slashes).
  *Accept:* manifest exists after a run, schema validated in a test.

- [x] **1.6 Document `Nothing` semantics** (P2)
  Under KeepBest it is a proper no-op; under KeepAll album copies still land as physical
  extra copies in `ALL_PHOTOS`. Document, don't change.
  *Accept:* README/cookbook explain the KeepAll caveat.

- [x] **1.7 EXIF-read date fallback** (P1)
  The `DateSource.Exif` tier is implemented and tested but unreachable — the pipeline
  passes `exifDateLocal: null`. Read `DateTimeOriginal` (images) / `CreationDate` (video)
  with the managed MetadataExtractor library after extraction and re-resolve when the
  sidecar produced no date. Opt-out via `--no-exif-fallback`.
  *Accept:* a file with EXIF `DateTimeOriginal` but no sidecar gets `DateSource=Exif` and
  the matching year/month folder; dry-run limitation (no extraction → no EXIF read)
  documented in troubleshooting.

## Phase 2 — Tests & quality hardening (target: v1.2.x)

- [x] **2.1 Unit tests for the CLI parser** (P1)
  `CliOptions.Parse` and `ToProcessingOptions` are untested and duplicated against the
  help text. Add `InternalsVisibleTo` + a test-project reference to the CLI project.
  *Accept:* ~20 cases covering defaults, every flag, enum aliases, and error paths.

- [x] **2.2 Default fallback timezone from the system** (P1)
  `Asia/Jerusalem` is hardcoded as the app-wide default. Derive the default from the
  system timezone (converted to IANA), keeping `Asia/Jerusalem` only in examples.
  *Accept:* unit test that the resolved default is a valid IANA id.

- [x] **2.3 `TryDelete` logs failures** (P2)
  Temp-file cleanup failures are currently swallowed silently.
  *Accept:* a warning with the path is logged on failure.

- [x] **2.4 Remove the dead `ExportReport` command stub** (P2)
  The RelayCommand is never bound (the window uses `Click="OnExportReport"`); delete the
  stub and its dead `NotifyCanExecuteChanged` call.

- [x] **2.5 Single-source the version number** (P2)
  `Directory.Build.props` says 1.1.0 while `Package.appxmanifest` says 1.0.0.0 — already
  drifted. Sync the manifest and add a CI step to the MSIX workflow that rewrites the
  manifest version from `Directory.Build.props`.

- [ ] **2.6 Raise the coverage gate 60 → 65 → 70** (P2)
  The 65 milestone shipped with 2.1 (current coverage ≈67%; the CLI's untested
  `Program.cs` entry point drags the average down). 70 becomes reachable once 3.1
  extracts testable logic from the App.

## Phase 3 — App polish (target: v1.3.0)

- [ ] **3.1 ViewModel testability** (P1) — extract pure logic (ETA formatting,
  index↔enum mapping, phase translation) from `MainViewModel` into plain classes testable
  on any OS; evaluate a windows-only VM test project on CI.
- [ ] **3.2 Per-file results viewer** (P2) — a grid of file outcomes on the summary step,
  filterable by error / duplicate / date source.
- [ ] **3.3 Expose advanced options in the App** (P2) — CPU/ExifTool parallelism and the
  EXIF-fallback toggle exist in the CLI but not in the wizard's options step.
- [ ] **3.4 Accessibility & RTL audit** (P2) — keyboard navigation, screen-reader labels,
  and a full RTL pass over the wizard.

## Phase 4 — Maintenance & finish line

- [ ] **4.1 Re-test single-file App publishing** on each new WindowsAppSDK release; the
  v1.1.0 startup crash is upstream
  ([microsoft/WindowsAppSDK#2597](https://github.com/microsoft/WindowsAppSDK/issues/2597)).
  Until fixed, the App ships as a zipped folder.
- [ ] **4.2 Refresh [docs/performance.md](docs/performance.md)** with measurements from a
  large real-world Takeout (100 GB+), including EXIF-fallback overhead.
- [ ] **4.3 Dependency & ExifTool policy** — document the update cadence for NuGet
  dependencies (dependabot already runs) and the pinned-ExifTool upgrade policy.
- [ ] **4.4 Ideas backlog** (explicitly out of scope for 1.x): unzipped-Takeout folder
  input, Motion Photo pairing (keep MP/MOV siblings together), partner-shared media
  segregation options, cross-platform CLI.
