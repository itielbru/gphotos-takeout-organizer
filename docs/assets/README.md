# Marketing assets

Screenshots used by the root `README.md` / `README.he.md`.

- `hero.png` — wizard Step 1, English (LTR). Root README hero.
- `wizard-en.png` — Step 1, English (LTR).
- `wizard-he.png` — Step 1, Hebrew (RTL).

The app renders with a dark **Mica** title bar (no app name/icon), so the
captures intentionally have a translucent dark caption bar.

## Regenerating

The window uses a transparent (Mica) title bar, so capture against the bare
desktop wallpaper for a clean, consistent backdrop:

1. Build + run: `dotnet build src/GPhotosTakeout.App/GPhotosTakeout.App.csproj -c Release -p:Platform=x64`, then launch the published `GPhotosTakeout.App.exe`.
2. Switch language via the **Language / שפה** picker (top corner) — or set `"Language": "en"` / `"he"` in `%LocalAppData%\GPhotosTakeout\settings.json` and relaunch.
3. Minimize all other windows (so Mica samples only the wallpaper) and capture the window's [`DWMWA_EXTENDED_FRAME_BOUNDS`](https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) rectangle, not the whole screen.

Keep images ≤ ~1450px wide so the README stays light.
