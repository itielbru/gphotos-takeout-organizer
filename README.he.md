<div align="right">

**עברית** · [English](README.md)

</div>

# מארגן Google Photos Takeout (WinUI3)

אפליקציית Windows שלוקחת קובצי **Google Photos Takeout** (ZIP), מאחדת את ה-metadata
מקובצי ה-JSON חזרה לתוך התמונות (EXIF/XMP), מתקנת תאריכים ואזורי זמן, מטפלת
בכפילויות ובאלבומים, ומפיקה ספרייה נקייה ומאורגנת. הממשק בעברית (RTL) ובאנגלית.

<div align="center">
<img src="docs/assets/wizard-he.png" alt="אשף מארגן Google Photos Takeout" width="760">
</div>

## התקנה מהירה

<div align="center">

[![הורד גרסה אחרונה](https://img.shields.io/github/v/release/itielbru/gphotos-takeout-organizer?label=%D7%94%D7%95%D7%A8%D7%93%20%D7%92%D7%A8%D7%A1%D7%94%20%D7%90%D7%97%D7%A8%D7%95%D7%A0%D7%94&style=for-the-badge&logo=windows&color=0D7377)](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest)

**Windows 10 / 11 · קובץ EXE יחיד · ללא התקנה · ללא ZIP**

</div>

1. הורד מ-[הגרסה האחרונה](https://github.com/itielbru/gphotos-takeout-organizer/releases/latest) — בחר את קובץ ה-**App** מסוג `.exe` המתאים למעבד שלך (x64 / ARM64). זהו קובץ יחיד עצמאי — בלי ZIP ובלי התקנה.
2. הרץ את `GPhotosTakeout-App-…-x64.exe`. בהרצה הראשונה לחץ על **התקן את ExifTool** כדי לאפשר כתיבת מטא-דאטה (הורדה חד-פעמית של ~10MB). כל שאר היכולות עובדות גם בלעדיו.
3. הוסף את קובצי ה-Takeout → בחר אפשרויות → הרץ.

> **הערת SmartScreen:** הקובץ אינו חתום דיגיטלית, ולכן בהרצה הראשונה Windows עשוי להציג
> אזהרה. לחץ **More info → Run anyway** כדי להמשיך.

## מבנה הפרויקט

```
GPhotosTakeout.sln
├─ src/GPhotosTakeout.Core/   לוגיקת העיבוד (ללא תלות ב-UI, נבדקת ב-unit tests)
│   ├─ Archives/   אינדוקס + streaming מתוך ה-ZIP, איחוד חלקים, נעילה פר-ארכיון
│   ├─ Matching/   התאמת מדיה↔JSON כולל Issue #353 (supplemental-metadata קטוע)
│   ├─ Metadata/   מנוע ExifTool ב-batch mode (-stay_open, פרוטוקול {ready}) + pool
│   ├─ Dates/      היררכיית תאריך + timezone מ-GPS (offset נכון)
│   ├─ Dedup/      זיהוי כפילויות אטומי מבוסס hash
│   ├─ Albums/     קישור אלבומים: symlink → hardlink → copy
│   ├─ IO/         תמיכת נתיבים ארוכים (\\?\)
│   └─ Pipeline/   אורקסטרציה, מקביליות, resume, progress
├─ src/GPhotosTakeout.App/    אפליקציית WinUI3 (Unpackaged, עברית/אנגלית)
├─ src/GPhotosTakeout.Cli/    הרצה headless (gptakeout) — אוטומציה, batch, בדיקות E2E
└─ tests/GPhotosTakeout.Tests/  87 בדיקות (matching, dates, dedup, pipeline, מקביליות,
                                ולידציה, dry-run, ExifTool resilience, long-path, archives, timezone, albums)
```

פרטי הארכיטקטורה המלאים: [ARCHITECTURE.md](ARCHITECTURE.md).

## דרישות

- **.NET 9 SDK** (לבנייה מהמקור).
- **ExifTool** — מותקן בלחיצה אחת מתוך האפליקציה בהרצה הראשונה (אל `%LocalAppData%\GPhotosTakeout\Tools`). לבנייה מהמקור, הורד את `exiftool.exe` מ-https://exiftool.org
  והנח אותו בתיקיית `Tools/` שליד קובץ ההרצה. ללא ExifTool, האפליקציה עדיין מארגנת
  ומתארכת קבצים, אך לא כותבת metadata לתוך הקבצים.

## בנייה והרצה

```powershell
# בדיקות (Core)
dotnet test tests/GPhotosTakeout.Tests/GPhotosTakeout.Tests.csproj

# בניית האפליקציה (חובה לציין פלטפורמה — x64 או ARM64)
dotnet build src/GPhotosTakeout.App/GPhotosTakeout.App.csproj -p:Platform=x64

# הרצה
./src/GPhotosTakeout.App/bin/x64/Debug/net9.0-windows10.0.19041.0/GPhotosTakeout.App.exe
```

## הרצה משורת פקודה (CLI)

```powershell
# תצוגה מקדימה (Dry-run) — מתכנן ומדווח בלי לכתוב כלום
dotnet run --project src/GPhotosTakeout.Cli -- -i takeout-001.zip -o C:\Out --dry-run --report plan.json

# ריצה אמיתית עם דוח CSV
dotnet run --project src/GPhotosTakeout.Cli -- -i takeout-001.zip -i takeout-002.zip -o C:\Out --report report.csv

# עזרה מלאה
dotnet run --project src/GPhotosTakeout.Cli -- --help
```

דגלים עיקריים: `--structure yearmonth|albums|flat`, `--albums`, `--duplicates`,
`--timezone <IANA>`, `--no-metadata`, `--exiftool <path>`, `--dry-run`, `--report <.json|.csv>`,
`--log <path>`, `--no-log`, `-v`. קודי יציאה: 0 הצלחה · 1 הושלם עם שגיאות · 2 קלט לא תקין · 3 בוטל.

## החלטות עיצוב מרכזיות

- **Unpackaged / Self-Contained** — כדי לאפשר הרצת `exiftool.exe` וגישה
  מלאה למערכת הקבצים (MSIX sandbox חוסם את שניהם). בניית MSIX נשארת אפשרית (opt-in).
- **ExifTool ב-batch mode מתמשך** — תהליך יחיד עם `-stay_open`, pool קטן
  (I/O-bound) במקום הרצה לכל קובץ.
- **התאמה לפי prefix, לא לפי אורך-קיטוע קבוע** — עמיד לכל וריאציה של גוגל
  (`.supplemental-metadata.json` ועד `.s.json`).
- **timezone מ-GPS** — `photoTakenTime` הוא UTC; מחושב זמן מקומי + `OffsetTimeOriginal`
  לתמונות, ו-`QuickTime:CreateDate` (UTC) לווידאו.
- **נתיבים ארוכים (>260)** — prefix `\\?\` + `longPathAware` ב-manifest.

## בעיות ותמיכה

דיווחי באגים ובקשות לתכונות נקראים ומטופלים ברצינות. אם משהו לא עובד — תאריכים שגויים, קבצים חסרים, קריסה — פתחו [issue](https://github.com/itielbru/gphotos-takeout-organizer/issues) עם פרטים: גרסת Windows, איזה Takeout גרם לבעיה, ושורות רלוונטיות מקובץ הלוג (`%LocalAppData%\GPhotosTakeout\logs\`). כל דיווח מקבל מענה.

לבעיות אבטחה — השתמשו ב[דיווח פרטי](https://github.com/itielbru/gphotos-takeout-organizer/security/advisories/new) במקום issue ציבורי.

## רישיון

[MIT](LICENSE).
