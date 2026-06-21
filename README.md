# מארגן Google Photos Takeout (WinUI3)

אפליקציית Windows שלוקחת קובצי **Google Photos Takeout** (ZIP), מאחדת את ה-metadata
מקובצי ה-JSON חזרה לתוך התמונות (EXIF/XMP), מתקנת תאריכים ואזורי זמן, מטפלת
בכפילויות ובאלבומים, ומפיקה ספרייה נקייה ומאורגנת. הממשק בעברית (RTL).

## מבנה הפרויקט

```
GPhotosTakeout.sln
├─ src/GPhotosTakeout.Core/   לוגיקת העיבוד (ללא תלות ב-UI, נבדקת ב-unit tests)
│   ├─ Archives/   אינדוקס + streaming מתוך ה-ZIP, איחוד חלקים, נעילה פר-ארכיון
│   ├─ Matching/   התאמת מדיה↔JSON כולל Issue #353 (supplemental-metadata קטוע)
│   ├─ Metadata/   מנוע ExifTool ב-batch mode (-stay_open, פרוטוקול {ready}) + pool
│   ├─ Dates/      היררכיית תאריך + timezone מ-GPS (offset נכון)
│   ├─ Dedup/      זיהוי כפילויות דו-שלבי (חתימה מהירה ← hash מלא)
│   ├─ Albums/     קישור אלבומים: symlink → hardlink → copy
│   ├─ IO/         תמיכת נתיבים ארוכים (\\?\)
│   └─ Pipeline/   אורקסטרציה, מקביליות, resume, progress
├─ src/GPhotosTakeout.App/    אפליקציית WinUI3 (Unpackaged, עברית RTL)
└─ tests/GPhotosTakeout.Tests/  57 בדיקות (matching, dates, dedup, pipeline, מקביליות)
```

## דרישות

- **.NET 9 SDK** (הותקן).
- **ExifTool** — הורד את `exiftool.exe` מ-https://exiftool.org והנח אותו בתיקיית
  `Tools/` שליד קובץ ההרצה (או ליד ה-exe). ללא ExifTool, האפליקציה עדיין מארגנת
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

## החלטות עיצוב מרכזיות

- **Unpackaged / Self-Contained** — לא MSIX, כדי לאפשר הרצת `exiftool.exe` וגישה
  מלאה למערכת הקבצים (MSIX sandbox חוסם את שניהם).
- **ExifTool ב-batch mode מתמשך** — תהליך יחיד עם `-stay_open`, pool קטן
  (I/O-bound) במקום הרצה לכל קובץ.
- **התאמה לפי prefix, לא לפי אורך-קיטוע קבוע** — עמיד לכל וריאציה של גוגל
  (`.supplemental-metadata.json` ועד `.s.json`).
- **timezone מ-GPS** — `photoTakenTime` הוא UTC; מחושב זמן מקומי + `OffsetTimeOriginal`
  לתמונות, ו-`QuickTime:CreateDate` (UTC) לווידאו.
- **נתיבים ארוכים (>260)** — prefix `\\?\` + `longPathAware` ב-manifest.

## סטטוס

- ✅ Core מלא + 57 בדיקות עוברות (כולל בדיקות מקביליות: dedup אטומי, מניעת איבוד קבצים).
- ✅ אפליקציית WinUI3 נבנית ורצה (wizard בעברית, 4 שלבים).
- ⏳ לבדיקה ידנית מלאה: הרצה על Takeout אמיתי עם ExifTool מותקן.
