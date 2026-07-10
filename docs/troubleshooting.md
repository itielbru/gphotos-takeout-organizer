# Troubleshooting & FAQ

## Dates are wrong or photos land in "Undated"

**Why:** Google's JSON sidecar contains a UTC timestamp. The tool converts it to local time using the photo's GPS coordinates (when available) or a fallback timezone you provide.

- **No GPS, no fallback** → timestamps stay as UTC, and the year/month folder is based on UTC midnight. Set `--timezone` (CLI) or the "Fallback timezone" field (app) to your local IANA ID (e.g. `America/New_York`).
- **GPS present but wrong zone** → this should resolve correctly via GeoTimeZone. If a remote/rural coordinate maps to the wrong zone, please open an issue with the coordinates.
- **No sidecar file at all** → the tool reads the capture date embedded in the file itself (EXIF `DateTimeOriginal` for photos, the QuickTime creation time for videos), then falls back to filename patterns (`IMG_20230815_*`, `VID_20230815_*`), the album-folder year, and finally the archive's file-modified timestamp. Disable the embedded-date read with `--no-exif-fallback` if you don't trust your files' EXIF clocks.
- **Undated folder** → intentional; do not delete it. Files in `Undated` had no recoverable date from any source.
- **Dry-run shows a weaker date source than the real run** → expected. `--dry-run` never extracts files, so it cannot read dates embedded in them; a file reported as `Filename`/`FileModified` in the dry-run may resolve as `Exif` in the real run.

---

## Files are missing from the output

**Possible causes:**

1. **Sidecar matching** — each `.jpg.supplemental-metadata.json` must sit in the same archive folder as its media file. If Google split your Takeout into multiple ZIPs, pass all of them at once: the tool merges their indexes before matching.
2. **Filename truncation** — Google truncates long filenames at 46 characters in the JSON `title` field. The matcher applies a fuzzy 46-char prefix match to handle this.
3. **Duplicate skip** — with `--duplicates keep-best` (default), identical files are de-duplicated. Check `report.json` (`"Duplicates"` field) to confirm this is the cause.
4. **Archive errors** — a corrupted ZIP entry is skipped and logged. Re-download the affected Takeout part from Google.

---

## Album entries (Photos from albums) are missing

The album linking (`--albums shortcut`) requires the canonical copy in `Photos from YYYY` to be placed **before** the album copy's duplicate resolution. If the canonical copy is in a different ZIP that wasn't provided, the album copy becomes the canonical instead.

Ensure all Takeout ZIPs are provided together as a single run.

---

## ExifTool doesn't start / metadata not written

**Symptoms:** Files are organized correctly but no EXIF metadata is embedded; the report shows `MetadataWritten: 0`.

**Fixes:**

1. **Wrong path** — check `--exiftool` (CLI) or the ExifTool path setting (app). Run `exiftool -ver` in a terminal to verify.
2. **Missing `exiftool_files/`** — the Windows standalone EXE requires a sibling `exiftool_files/` directory. Use the in-app installer ("Install ExifTool") or download the full ZIP from exiftool.org.
3. **Permissions** — the EXE must be executable. Unblock it via Properties → Unblock (Windows).
4. **Timeout** — very large runs (100K+ files) can hit the 5-minute ExifTool timeout. Increase via `--exif-timeout` (CLI). The default is 300 seconds.
5. **Antivirus** — some AV products quarantine or slow exiftool.exe. Add an exception for the Tools folder.

---

## Live Photo — the video component didn't get metadata

Live Photos produce two files: `.jpg` + `.mp4` (or `.mov`). Google provides one sidecar for the pair (usually matched to the `.jpg`). The `.mp4` is matched separately; if there's no sidecar for it, dates come from the filename pattern.

This is expected behavior. The video component gets the same date as the image when the sidecar is present; otherwise it lands in `Undated`.

---

## "Access denied" when creating album shortcuts (symlinks)

Windows requires **Developer Mode** (Settings → For developers → Developer Mode) or administrator privileges for symbolic links. When not available, the tool automatically falls back to:

1. Hard links (same drive, no copy)
2. File copy (cross-drive or when hard links fail)

There is no functional difference except disk space usage with copies.

---

## CLI exit codes

| Code | Meaning |
|------|---------|
| 0 | Success — all files processed |
| 1 | Partial success — some files had errors (see report) |
| 2 | Invalid arguments or missing input |
| 3 | Fatal error — unhandled exception |
| 64 | Cancelled by user (Ctrl+C) |

---

## Resuming an interrupted run

If a run is interrupted, re-run with the **same output directory**. The tool reads `.gphotos-resume.log` in the output folder and skips already-processed files. Delete this file to force a full re-run from scratch.

---

## I accidentally ran twice — how to clean up duplicates

Run again with `--duplicates keep-best` (the default). Already-placed files will be re-checked against the content-hash journal. Files that appear more than once in the output will have ` (1)`, ` (2)` suffixes — search for these patterns to find them.

Alternatively, delete the output directory and re-run from scratch.
