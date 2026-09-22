# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Static HTML/CSS for the GitHub Pages landing (`index.html` + `index.he.html`), no framework,
no build step; served as-is by GitHub Pages (Jekyll) from `main`. The product itself is a
Windows app (WinUI 3 + CLI, C#/.NET 9); the web surface is only its landing page.

## Users

People who exported their Google Photos library with Google Takeout and want a usable local
photo library on Windows. Two groups, same job:

- **Non-technical** — ran Takeout once (leaving Google, backing up, moving to a NAS or another
  photo app), got dozens of ZIPs, and found every photo dated "today" with `.json` files
  everywhere. Wants one download and a wizard.
- **Technical / data hoarders** — know what EXIF and sidecars are, may script it, care about
  correctness (timezone offsets, dedup safety, resumability) and compare tools before choosing.

Both arrive from a search or a Reddit/GitHub thread about "Takeout wrong dates / json files".
Hebrew speakers are a confirmed audience (bilingual UI and README).

## Product Purpose

Reads Google Takeout ZIPs directly and writes each photo's real capture time (with timezone
offset), GPS and description from the JSON sidecar back into the file's EXIF/XMP, de-duplicates
by content, recreates albums, and organizes into a year/month library. Success: the user opens
the output folder in any photo app and everything is dated, located and sorted correctly, in
minutes, without losing anything.

## Positioning

- Writes the metadata **into the files** (not just renames/sorts), including
  `OffsetTimeOriginal` derived from GPS — most alternatives write the date only, or none.
- Reads multi-part ZIPs directly; no "unzip and merge 200 GB first".
- Handles Google's truncated `*.supplemental-metad*.json` names by prefix (the open pain point
  of the most popular alternative, GooglePhotosTakeoutHelper #353).
- Windows-native wizard **and** CLI; Hebrew (RTL) and English UI switchable live.
- Free, MIT, no telemetry, fully offline. Nearest alternatives: GooglePhotosTakeoutHelper
  (cross-platform CLI, date only), immich-go (uploads to Immich, not a fixer),
  google-photos-exif (DateTimeOriginal only).

## Operating Context

Windows 10/11. Input: Takeout ZIPs (often many, multi-part). Output: a folder tree
(`ALL_PHOTOS/yyyy/yyyy-MM`, `Albums/`, special folders). Installer is per-user, no admin.
ExifTool is bundled. Users verify results by opening the output in Windows Photos / Lightroom /
Immich etc.

## Capabilities and Constraints

- Structures: year/month, albums, flat. Album strategies: shortcut (symlink→hardlink→copy),
  duplicate, JSON manifest, none. Duplicate handling: keep best / keep all. Dry-run. Resumable.
  Per-file CSV/JSON report.
- **Windows only.** No macOS/Linux build (undecided whether a Linux CLI is worth it).
- **Installer is not code-signed** — SmartScreen warns on first run ("More info → Run anyway").
  SignPath Foundation application was declined (2026-09-22) for lack of public traction; will
  reapply. This must be stated honestly on the landing page, not hidden.
- winget package submitted (`itielbru.GPhotosTakeoutOrganizer`, microsoft/winget-pkgs#438719),
  awaiting moderator — not yet installable via winget; do not claim it until merged.
- Download artifacts per release: `GPhotosTakeout-Setup-win-x64.exe` (installer, stable name,
  `releases/latest/download/…`), portable App zip, CLI exe, `sbom.spdx.json`, `SHA256SUMS.txt`.
- Terminology: "Takeout", "sidecar JSON", "ExifTool", "Albums", "ALL_PHOTOS".

## Brand Commitments

- Name: **Google Photos Takeout Organizer** (Hebrew: מארגן Google Photos Takeout). Not affiliated
  with Google; never imply endorsement.
- Existing accent: teal `#0D7377` (README badge, social card); the app UI is dark with teal
  accent buttons. Not a binding palette for the landing page, but the product screenshots carry it.
- Voice (README): direct, technical-honest, no hype; caveats stated plainly.
- Author/publisher: itielbru (GitHub).

## Evidence on Hand

- `docs/assets/wizard.gif` — 6-frame walkthrough of the real app (880 px, 346 KB).
- `docs/assets/wizard-en.png`, `wizard-he.png`, `hero.png` — screenshots (1426×742).
- `docs/assets/social-preview.png` — 1280×640 card.
- Comparison table vs three alternatives (README "How it compares", facts dated Sept 2026).
- 192 automated tests; CI + CodeQL badges; SBOM and checksums on every release.
- One real user bug report fixed and released within a day (#26 → v1.3.2).
- **Absent, do not fabricate:** testimonials, user counts, download numbers, star counts worth
  showing (3), press, benchmarks.

## Product Principles

1. Show the real product doing the real job; no stock imagery, no mock UIs.
2. One primary action: download the installer. Everything else is secondary.
3. State the caveats (unsigned, Windows-only) as plainly as the features.
4. Serve both readers: a non-technical path in the first screen, the technical proof below.
5. Hebrew is a first-class page, not a translation afterthought (true RTL).

## Accessibility & Inclusion

Bilingual LTR/RTL. Keyboard-navigable, visible focus, sufficient contrast on the dark
product palette; the GIF must not be the only carrier of information (text equivalents).
