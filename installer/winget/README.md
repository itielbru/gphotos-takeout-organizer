# winget manifest

Source of the `itielbru.GPhotosTakeoutOrganizer` package in
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs/tree/master/manifests/i/itielbru/GPhotosTakeoutOrganizer).

Per release, after the GitHub release is published:

1. Update `PackageVersion`, `InstallerUrl`, `InstallerSha256` (from the release's `SHA256SUMS.txt`,
   upper-case), `ReleaseDate` and `ReleaseNotesUrl` in the three files.
2. `winget validate --manifest installer/winget`
3. Copy the three files to `manifests/i/itielbru/GPhotosTakeoutOrganizer/<version>/` in a fork of
   winget-pkgs and open a PR titled `Update: itielbru.GPhotosTakeoutOrganizer version <version>`
   (or use `wingetcreate update itielbru.GPhotosTakeoutOrganizer -u <InstallerUrl> -v <version> --submit`).

`ProductCode` is the Inno Setup `AppId` + `_is1` and never changes.
