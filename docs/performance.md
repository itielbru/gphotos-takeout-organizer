# Performance Tuning Guide

## Defaults

| Flag | Default | Description |
|------|---------|-------------|
| `--cpu N` | max(2, cores − 2) | Parallel extraction + placement threads |
| `--exif-parallel N` | 4 | Concurrent ExifTool processes |
| `--exif-timeout N` | 300 | Seconds before ExifTool is considered hung |

---

## When to adjust `--cpu`

`--cpu` controls how many files are processed simultaneously. Each slot extracts a ZIP entry, resolves its date, and moves it to the output tree.

- **Local NVMe → local NVMe** — use the default (close to core count). Disk throughput is rarely the bottleneck.
- **Source is a network share (NAS/SMB)** — lower to 2–4. Too many concurrent reads on SMB saturates the connection and causes timeouts.
- **Output is a network share** — lower to 2–4. Write latency dominates; more threads just queue up.
- **Very large dataset (100K+ files)** — default is fine; the bottleneck shifts to ExifTool (`--exif-parallel`).

```powershell
# Conservative for NAS targets
gptakeout -i takeout.zip -o \\nas\Photos --cpu 2 --exif-parallel 2
```

---

## When to adjust `--exif-parallel`

ExifTool runs in persistent `-stay_open` mode. Each parallel slot is an independent OS process. Increasing this is only beneficial when:

- You have many CPU cores available (8+)
- Metadata writing is the measured bottleneck (check CPU % during a run)
- ExifTool processes aren't bottlenecked on disk I/O

Beyond 8 parallel slots the marginal gain is small and memory usage increases.

```powershell
# High-core machine with local SSD
gptakeout -i takeout.zip -o D:\Photos --cpu 12 --exif-parallel 8
```

---

## Dataset-size guidelines

### < 10 000 files

Use defaults. A full run typically completes in under 5 minutes even on modest hardware.

### 10 000 – 100 000 files

Use defaults, but monitor ExifTool. If metadata writing is slow, increase `--exif-parallel 6`.

If the run is interrupted, re-run with the same output directory — the resume journal skips already-done files.

### 100 000+ files

- Increase `--exif-timeout 600` (10 min) to avoid premature timeout on slow storage.
- Consider splitting into multiple runs by Takeout zip group if memory pressure is an issue (each archive set is indexed in memory).
- Use `--report json` and inspect `MetadataWritten` vs `TotalMedia` to diagnose ExifTool throughput.

---

## Long paths (Windows)

The tool writes `\\?\`-prefixed paths internally, so paths longer than 260 characters work on NTFS without any Windows setting change.

**Caveats:**
- Long paths do **not** work on FAT32 or exFAT (SD cards, USB drives formatted as FAT).
- Long paths over SMB work only when both the server and client support them (Server 2016+ and Windows 10 1607+).
- Windows Explorer and some older apps cannot open files with very long paths even when they exist. Use PowerShell or a long-path–aware file manager.

---

## Memory usage

The archive index (list of all entries in all ZIPs) is held in memory. A 100-archive Takeout export with 200K entries uses roughly 150–300 MB of heap.

If you hit `OutOfMemoryException`, split the ZIPs into smaller batches and run multiple times (the resume journal ensures no file is processed twice).

---

## Disk space during processing

Each file is extracted to a `.part` temp file beside its final destination before moving it atomically. Peak extra disk usage equals the largest single file in the archive. The `.part` file is always deleted after the move (or on error).
