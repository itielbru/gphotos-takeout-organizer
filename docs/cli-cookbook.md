# CLI Cookbook

Common recipes for `gptakeout` — the headless command-line interface.

---

## Basic run

```powershell
gptakeout `
  -i "C:\Downloads\takeout-20230815T000000Z-001.zip" `
  -i "C:\Downloads\takeout-20230815T000000Z-002.zip" `
  -o "D:\Photos"
```

All Takeout ZIPs must be passed together so sidecar matching works across archive boundaries.

---

## Preview without writing files (dry run)

```powershell
gptakeout `
  -i takeout-001.zip `
  -o D:\Photos `
  --dry-run
```

Prints a plan: where each file would go, which would be skipped as duplicates. Nothing is written to disk.

---

## Set a fallback timezone

```powershell
gptakeout -i takeout.zip -o D:\Photos --timezone "America/New_York"
```

Used when a photo has no GPS data. Accepts any IANA timezone ID.

---

## Keep all duplicate copies

```powershell
gptakeout -i takeout.zip -o D:\Photos --duplicates keep-all
```

Disables de-duplication. Identical files in multiple albums each get their own copy with a unique suffix (e.g. `IMG_001 (1).jpg`).

---

## Choose output structure

```powershell
# Year/month tree (default)
gptakeout -i takeout.zip -o D:\Photos --structure yearmonth

# All files flat in one folder
gptakeout -i takeout.zip -o D:\Photos --structure flat

# Per-album folders
gptakeout -i takeout.zip -o D:\Photos --structure albums
```

---

## Album strategies

```powershell
# Symlink / hardlink / copy fallback (default)
gptakeout -i takeout.zip -o D:\Photos --albums shortcut

# Duplicate every album photo (safest, most disk space)
gptakeout -i takeout.zip -o D:\Photos --albums duplicate

# No Albums folder — write an albums.json manifest at the output root instead
gptakeout -i takeout.zip -o D:\Photos --albums json

# No album output at all
gptakeout -i takeout.zip -o D:\Photos --albums nothing
```

The `json` manifest maps each album to its files, with paths relative to the output
root (forward slashes), so it survives moving the library:

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-07-10T12:00:00Z",
  "albums": [
    {
      "name": "Trip to Eilat",
      "files": [
        { "fileName": "IMG_1234.jpg", "path": "ALL_PHOTOS/2023/2023-08/IMG_1234.jpg" }
      ]
    }
  ]
}
```

Note: with `--duplicates keepall`, identical album copies are *also* kept as physical
files in `ALL_PHOTOS` (with a `(1)` suffix) in addition to the album materialization —
`keepall` disables de-duplication entirely.

---

## Write a JSON report

```powershell
gptakeout -i takeout.zip -o D:\Photos --report json
```

Produces `report.json` in the output directory with per-file outcomes.

### Extracting stats with PowerShell

```powershell
$r = Get-Content D:\Photos\report.json | ConvertFrom-Json
"Total: $($r.TotalMedia)  Errors: $($r.Errors)  Duplicates: $($r.Duplicates)"
```

### Filtering errors

```powershell
$r = Get-Content D:\Photos\report.json | ConvertFrom-Json
$r.ErrorMessages | ForEach-Object { $_ }
```

---

## Write a CSV report

```powershell
gptakeout -i takeout.zip -o D:\Photos --report csv
```

Produces `report.csv` — importable in Excel. Columns: `FileName`, `SourceFolder`, `DestinationPath`, `DateSource`, `Matched`, `IsDuplicate`, `Error`.

---

## Tuning parallelism

```powershell
# Use 8 CPU threads and 4 ExifTool processes
gptakeout -i takeout.zip -o D:\Photos --cpu 8 --exif-parallel 4
```

Default: `--cpu` = max(2, logical cores − 2), `--exif-parallel` = 4.

For I/O-bound NAS targets, lowering `--cpu` often improves throughput. For CPU-bound workloads (large RAW files + metadata writes), raising `--exif-parallel` helps.

---

## Automation: process multiple Takeout downloads in a loop (PowerShell)

```powershell
$archives = Get-ChildItem "C:\Downloads" -Filter "takeout-*.zip" | Select-Object -ExpandProperty FullName
$archiveArgs = $archives | ForEach-Object { "-i", $_ }

gptakeout @archiveArgs -o "D:\Photos" --timezone "Asia/Jerusalem" --report json

if ($LASTEXITCODE -ne 0) {
    Write-Error "gptakeout exited with code $LASTEXITCODE"
    exit $LASTEXITCODE
}
```

---

## Automation: process in a Bash loop (WSL / macOS)

```bash
#!/usr/bin/env bash
set -euo pipefail

ARCHIVES=(~/Downloads/takeout-*.zip)
INPUTS=()
for f in "${ARCHIVES[@]}"; do INPUTS+=(-i "$f"); done

gptakeout "${INPUTS[@]}" -o ~/Photos --timezone "Europe/London" --report json

echo "Done. Exit: $?"
```

---

## Resume an interrupted run

Simply re-run the exact same command. The tool reads `.gphotos-resume.log` from the output directory and skips already-processed files automatically.

```powershell
# First run (interrupted)
gptakeout -i takeout.zip -o D:\Photos

# Second run (resumes where interrupted)
gptakeout -i takeout.zip -o D:\Photos
```

To force a full re-run:

```powershell
Remove-Item "D:\Photos\.gphotos-resume.log"
gptakeout -i takeout.zip -o D:\Photos
```

---

## Exit code handling in scripts

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Partial success (some files had errors) |
| 2 | Invalid arguments / missing input |
| 3 | Fatal unhandled exception |
| 64 | Cancelled (Ctrl+C) |

```powershell
gptakeout -i takeout.zip -o D:\Photos
switch ($LASTEXITCODE) {
    0  { Write-Host "All done." }
    1  { Write-Warning "Completed with errors — check report.json" }
    2  { Write-Error "Bad arguments"; exit 2 }
    64 { Write-Warning "Cancelled by user" }
    default { Write-Error "Fatal error $LASTEXITCODE"; exit $LASTEXITCODE }
}
```
