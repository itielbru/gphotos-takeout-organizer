# Security Policy

## Privacy: no telemetry, local-only

This tool processes your **personal photo library entirely on your machine**. It does **not**
send any data anywhere:

- **No telemetry**, analytics, or "phone home" of any kind.
- **No network access** for processing. (Building a release downloads a pinned ExifTool from
  exiftool.org via CI; the shipped app itself does not require the network.)
- Logs and crash dumps are written **locally** to
  `%LocalAppData%\GPhotosTakeout\logs\` and are never transmitted. If you hit a crash, you can
  attach that dump to a GitHub issue yourself.

## Reporting a vulnerability

Please report security issues **privately** rather than opening a public issue:

- Use [GitHub's private vulnerability reporting](https://github.com/itielbru/gphotos-takeout-organizer/security/advisories/new), or
- Email **bru.itiel@gmail.com** with details and reproduction steps.

You can expect an acknowledgement within a reasonable time. Please give us a chance to release
a fix before public disclosure.

## Supported versions

The latest released version receives security fixes.
