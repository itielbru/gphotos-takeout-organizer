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

## Code signing policy

Release binaries are built by the public [Release workflow](.github/workflows/release.yml)
on GitHub Actions from a tagged commit on `main`; nothing is built or signed on a
developer machine. Each release ships `SHA256SUMS.txt` alongside the artifacts.

Free code signing is provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/). Until that integration ships (tracked in
[#41](https://github.com/itielbru/gphotos-takeout-organizer/issues/41)), releases are
unsigned and Windows SmartScreen shows a warning on first run — see the README.

**Team and roles** (all held by the maintainer, [@itielbru](https://github.com/itielbru)):

- *Committers / authors* — direct commit access to this repository.
- *Reviewers* — review external contributions before they are merged; every change to
  `main` goes through a pull request with passing CI (enforced by a branch ruleset).
- *Approvers* — authorize each signing request for a release build.

**Privacy:** the signed software does not transmit any data; see *Privacy* above.

## Reporting a vulnerability

Please report security issues **privately** rather than opening a public issue:

- Use [GitHub's private vulnerability reporting](https://github.com/itielbru/gphotos-takeout-organizer/security/advisories/new), or
- Email **bru.itiel@gmail.com** with details and reproduction steps.

You can expect an acknowledgement within a reasonable time. Please give us a chance to release
a fix before public disclosure.

## Supported versions

The latest released version receives security fixes.
