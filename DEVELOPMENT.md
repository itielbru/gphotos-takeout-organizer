# יומן פיתוח — מארגן Google Photos Takeout

מסמך זה מתעד את כל מה שנעשה בפרויקט: המחקר, ההחלטות, סבבי הביקורת ואיך טופלו,
הארכיטקטורה, הבאגים שנמצאו ותוקנו, והאימות. הוא נכתב כדי שכל מפתח (או אני בעתיד)
יוכל להבין *למה* הקוד נראה כפי שהוא נראה.

---

## 1. המטרה

אפליקציית WinUI3 בעברית שלוקחת קובצי **Google Photos Takeout** (ZIP), מאחדת את
ה-metadata מקובצי ה-JSON חזרה לתוך התמונות (EXIF/XMP), מתקנת תאריכים ואזורי זמן,
מטפלת בכפילויות ואלבומים, ומפיקה ספרייה נקייה ומאורגנת לייבוא לכל אפליקציית תמונות.

---

## 2. מחקר (GitHub + רשת)

נסקרו הכלים הקיימים כדי ללמוד מה עובד, מה שבור, ואילו מקרי-קצה מפילים כלים.

### כלים מרכזיים שנבדקו
| כלי | מסקנה |
|------|--------|
| **GPTH** (TheLastGimbus, 160K+ הורדות) | נטוש מ-2023, **שבור** על פורמט ה-JSON החדש, ו**רק מעדכן filesystem timestamp במקום לכתוב EXIF** — אפליקציות רבות מתעלמות מזה |
| **GPTH Neo** (Xentraxx) | הפורק המודרני הפעיל; משתמש ב-**ExifTool**, resume, אסטרטגיות אלבומים, dedup ב-hashing — ה-reference הקרוב ביותר |
| **Wacheee fork** | fork של TheLastGimbus שפתר את Issue #353 (אומת ישירות מ-GitHub) |
| gophix / go-out / metadatafixer | מאששים: **ExifTool הוא gold-standard** לכתיבה |

### הבעיה הקשה ביותר — Issue #353
ב-2024 גוגל שינתה את שמות קובצי ה-JSON מ-`IMG.jpg.json` ל-`IMG.jpg.supplemental-metadata.json`,
ולעיתים **קוטעת את השם באמצע** (`.supplemental-metad.json`, `.supplem.json`, עד `.s.json`).
מקורות סותרים על אורך הקיטוע (46 מול 47 תווים) — ולכן **לא מקודדים מספר קסם**, אלא
מתאימים לפי **prefix**.

### מקרי-קצה נוספים שזוהו
- חוסר עקביות בסיומת (ה-JSON לפעמים כולל סיומת מדיה, לפעמים לא).
- `-edited` ו-`(1)` — גרסאות ערוכות וכפילויות ממוספרות.
- **Live/Motion Photos** — רק קובץ התמונה מקבל JSON; הווידאו האח חסר.
- פיצול בין מספר ZIP-ים; זוג מדיה↔JSON עלול ליפול בין שני ארכיונים.
- אובדן תיאורים/כיתובים, timestamps שגויים, ZIP פגום.

מקורות מלאים: ראה את סעיף "מקורות" בקובץ התוכנית.

---

## 3. שלושה סבבי ביקורת מומחה — ואיך טופל כל אחד

הפרויקט עבר שלושה סבבי ביקורת ארכיטקטונית. **כל הנקודות אומתו ושולבו.**

### סבב 1 — תיקוני יסוד
| הערה | מה נעשה |
|------|---------|
| ExifTool צריך **קובץ פיזי**, לא pipe מה-ZIP | חילוץ on-demand ישירות ליעד → כתיבה in-place (לא streaming ל-ExifTool) |
| מגבלת **MAX_PATH 260** | `LongPath` עם prefix `\\?\` + `longPathAware` ב-manifest |
| כתיבה היא **I/O-bound** | מקביליות מופרדת: CPU לשלבים מהירים, pool קטן (2–8) ל-ExifTool |
| **וידאו = תגיות אחרות** | `QuickTime:CreateDate` (UTC) במקום EXIF |
| תוספות שלי | UTF-8 לשמות עברית, `-overwrite_original`, כתיבה לקובץ וידאו של Live Photos |

### סבב 2 — אימות עובדתי
אומת ש-Issue #353 והפורק של Wacheee אמיתיים; זוהו אי-דיוקים (מספר גרסה מיושן, gist
לא מאומת). המסקנה המעשית: **בונים מאפס**, והפורקים משמשים כ-reference. חיזק את
ההחלטה להתאים לפי prefix ולא לפי אורך-קיטוע.

### סבב 3 — חסינות תקלות (Bulletproof)
| הערה | מה נעשה |
|------|---------|
| **Unpackaged deployment** (לא MSIX) | `WindowsPackageType=None` + `WindowsAppSDKSelfContained` — Full Trust להרצת ExifTool וגישה לקבצים |
| פרוטוקול **`{ready}`** ב-`-stay_open` | קריאת StdOut/StdErr אסינכרונית + `TaskCompletionSource`, `SemaphoreSlim` |
| `-api LargeFileSupport=1`, `QuickTimeUTC=1` | נוספו (קריטי לווידאו MP4/MOV גדול) |
| **timezone מ-GPS** → `OffsetTimeOriginal` | `TimezoneResolver` (GeoTimeZone → IANA), זמן מקומי + offset |
| `\\?\` ידני (ZipFile.ExtractToFile נכשל) | `LongPath.Create/OpenRead/Move` עם FileStream ידני |
| **symlink → hardlink → copy** fallback | `AlbumLinker` (symlinks דורשים Developer Mode) |
| **Hash דו-שלבי** | בוצע (וראה "שיפורי concurrency" למטה) |
| Progress לפי ספירת `{ready}` | ה-pipeline מחשב total ומעדכן לפי קבצים שעובדו |

---

## 4. ארכיטקטורה

הפרדה נקייה בין **Core** (לוגיקה נטולת-UI, נבדקת ב-unit tests) ל-**App** (WinUI3).

```
GPhotosTakeout.sln
├─ src/GPhotosTakeout.Core/   (.NET 9 class library)
├─ src/GPhotosTakeout.App/    (WinUI3, Unpackaged, עברית RTL)
└─ tests/GPhotosTakeout.Tests/ (xUnit — 57 בדיקות)
```

### רכיבי Core (קובץ → תפקיד)
| קובץ | תפקיד |
|------|--------|
| `Matching/FilenameNormalizer.cs` | שחזור שם מדיה מ-JSON (Issue #353); נרמול `-edited`/`(N)` לפי prefix |
| `Matching/SidecarMatcher.cs` | אינדקס גלובלי cross-folder; ירושת sibling ל-Live/Motion Photos |
| `Models/TakeoutJson.cs` | מודל ה-JSON: `photoTakenTime`, `geoData`, `description`, `favorited`; `CapturedUtc`, `BestGeo` |
| `Models/TakeoutEntry.cs`, `MatchResult.cs`, `ProcessingOptions.cs` | מודלים ואפשרויות (מבנה פלט, אלבומים, כפילויות, timezone) |
| `Dates/DateResolver.cs` | היררכיית תאריך: JSON → EXIF → שם קובץ (IMG_/PXL_/Screenshot/WhatsApp) → תיקייה → mtime |
| `Dates/TimezoneResolver.cs` | GPS→IANA (GeoTimeZone), זמן מקומי + offset, fallback ל-timezone-בית |
| `Metadata/ExifMetadata.cs` | מודל ה-metadata לכתיבה |
| `Metadata/ExifToolBatchWriter.cs` | ExifTool `-stay_open`, פרוטוקול `{ready}`, תגיות לפי פורמט, דגלי UTF-8/LargeFileSupport |
| `Metadata/ExifToolPool.cs` | pool מוגבל של תהליכי ExifTool; `DrainErrors` לחשיפת stderr |
| `Dedup/HashDeduplicator.cs` | dedup מבוסס hash, **אטומי ו-race-free** (ראה למטה) |
| `Albums/AlbumLinker.cs` | symlink → hardlink → copy עם דיווח |
| `IO/LongPath.cs` | תמיכת נתיבים ארוכים (`\\?\`) |
| `Archives/TakeoutArchiveReader.cs` | אינדוקס + streaming מ-ZIP, איחוד חלקים, **נעילה פר-ארכיון**, hash תוך כדי חילוץ |
| `Pipeline/OutputPathBuilder.cs` | בניית נתיב פלט (year/month / אלבומים / flat) + זיהוי תיקיות מיוחדות (Archive/Trash/Locked) |
| `Pipeline/ResumeJournal.cs` | journal append-only ל-resync של ריצה שנקטעה (נעול ל-thread-safety) |
| `Pipeline/ProcessingPipeline.cs` | אורקסטרציה: index → match → extract → dedup → metadata → link; progress + ביטול |

### רכיבי App (WinUI3)
| קובץ | תפקיד |
|------|--------|
| `App.xaml(.cs)` | אתחול, יצירת חלון |
| `MainWindow.xaml(.cs)` | Wizard ב-4 שלבים, RTL, file/folder pickers עם window handle |
| `ViewModels/MainViewModel.cs` | MVVM (CommunityToolkit), הרצת pipeline, progress דרך `DispatcherQueue` |
| `Services/ExifToolLocator.cs` | איתור אוטומטי של `exiftool.exe` ב-`Tools/` |
| `app.manifest` | `longPathAware`, `dpiAwareness` |

---

## 5. מודל ה-concurrency (חשוב)

עיבוד של 10K–100K+ קבצים חייב מקביליות, אבל מקביליות פתחה מספר מלכודות שטופלו:

- **ZipArchive לא thread-safe** → `TakeoutArchiveReader` מחזיק `SemaphoreSlim` פר-ארכיון:
  ארכיונים שונים נקראים במקביל, אך בתוך ארכיון בודד הקריאה מסונכרנת.
- **dedup אטומי** → `HashDeduplicator` מבוסס `ConcurrentDictionary` עם
  claim-based ownership (`TryClaim`/`PublishOwnerPath`/`FailOwner`): בדיוק thread אחד
  "מנצח" על כל hash; כפילויות מחכות לנתיב הקנוני. **ללא race ו-ללא איבוד קבצים**
  אם ה-owner נכשל.
- **hash תוך כדי חילוץ** → ה-SHA-256 מחושב בזמן ה-copy מה-ZIP, כך ש-dedup לא דורש
  קריאת דיסק נוספת.
- **ResumeJournal נעול** → קריאה/כתיבה ל-`HashSet` תחת אותו gate (Contains מול Add
  בו-זמנית = undefined behavior).
- **מקביליות מופרדת** → CPU-bound (matching/hashing) במקביל מלא; ExifTool ב-pool קטן
  (I/O-bound) למניעת thrashing.
- **שגיאות ExifTool נחשפות** → `ExifToolPool.DrainErrors()` נאסף ל-`ProcessingReport`
  בסוף ריצה, אחרת כשלי כתיבה היו בלתי-נראים.

---

## 6. באגים שנמצאו ע"י הבדיקות ותוקנו

1. **לולאת נרמול** — `IMG-edited(1).jpg` לא נורמל נכון; `-edited` ו-`(N)` יכולים
   לבוא בכל סדר → לולאה עד התייצבות.
2. **סיווג sibling** — וידאו `.MP` שמתאים ל-JSON של ה-`.jpg` סווג שגוי; תוקן לזיהוי
   לפי הבדל סיומת.
3. **באג קריטי — דריסת הקובץ הקנוני** — בארגון year/month, עותק האלבום מתמפה לאותו
   נתיב יעד כמו הקובץ הראשי; החילוץ דרס את הקנוני ואז dedup מחק אותו. **תוקן**:
   חילוץ ל-temp → dedup → move-into-place עם פתרון התנגשות שמות (` (1)`).
4. **converter ב-`x:Bind` על `Window`** — `Window` אינו `FrameworkElement`, ו-
   converter ב-compiled binding על שורש Window יצר קוד שגוי; תוקן ע"י חשיפת
   properties מסוג `Visibility` מה-ViewModel.

---

## 7. אימות

| בדיקה | תוצאה |
|-------|--------|
| `dotnet test` | ✅ **57/57 עוברות** (matching, dates, JSON, ExifTool args, dedup, pipeline integration, concurrency) |
| `dotnet build` (App, x64) | ✅ **0 warnings, 0 errors** |
| שיגור האפליקציה | ✅ התהליך חי עם חלון (bootstrapper של Unpackaged WinUI3 עובד) |
| בדיקת אינטגרציה | ✅ pipeline על ZIP סינתטי אמיתי: ארגון year/month, dedup של עותק אלבום, resume |

**סביבה:** .NET 9 SDK (9.0.315) הותקן דרך winget. WindowsAppSDK 2.2.0, CommunityToolkit.Mvvm 8.4.2, GeoTimeZone 5.3.0.

---

## 8. סטטוס נוכחי ומה שנותר

**הושלם:**
- ✅ Core מלא + 57 בדיקות עוברות, כולל חסינות concurrency.
- ✅ אפליקציית WinUI3 נבנית ורצה (wizard בעברית RTL, 4 שלבים).
- ✅ **ExifTool 13.59 הותקן ואומת** (`-ver` → 13.59) — כתיבת metadata פעילה (ראה יומן סשן 2026-06-21).
- ✅ האפליקציה הופעלה ידנית; חלון "מארגן Google Photos Takeout" עלה תקין (ללא קריסה).

**נותר (לא חוסם):**
- ⏳ העתקת ExifTool לתיקיית מקור קבועה `src/GPhotosTakeout.App/Tools/` + כלל `.csproj`
  להעתקה בכל בנייה. כרגע ההתקנה יושבת ב-build output בלבד — `dotnet clean`/מחיקת `bin` תמחק אותה.
- ⏳ בדיקה ידנית מלאה על Takeout אמיתי + פתיחת הפלט באפליקציית תמונות לאימות EXIF.
- 💡 שיפורים עתידיים: מחרוזות ב-`Resources.resw` (כרגע inline בעברית), קריאת EXIF
  קיים לזיהוי תאריך, התקדמות חלקה יותר ל-ExifTool.

---

## 9. בנייה והרצה

```powershell
# בדיקות
dotnet test tests/GPhotosTakeout.Tests/GPhotosTakeout.Tests.csproj

# בניית האפליקציה (חובה פלטפורמה — x64 או ARM64)
dotnet build src/GPhotosTakeout.App/GPhotosTakeout.App.csproj -p:Platform=x64

# הרצה
./src/GPhotosTakeout.App/bin/x64/Debug/net9.0-windows10.0.19041.0/GPhotosTakeout.App.exe
```

> הערה לסביבה הזו: `dotnet` **אינו** ב-PATH. יש לקדם את הפקודות ב-PowerShell עם
> `$env:Path = "C:\Program Files\dotnet;$env:Path"`.

---

## 10. יומן סשן — 2026-06-21

### 10.1 סשן דיבאגינג ושיפור עמוק (concurrency)

נקראה כל בסיס הקוד (Core + App + בדיקות). זוהו ותוקנו חמישה באגים — רובם מתעוררים
**רק תחת ההרצה המקבילה של ברירת המחדל**, ולכן הבדיקה האינטגרטיבית הקיימת (שרצה
ב-`CpuParallelism = 1`) לא תפסה אותם.

| # | חומרה | באג | תיקון |
|---|-------|-----|-------|
| 1 | 🔴 גבוה | `ResumeJournal.IsDone()` קרא `HashSet.Contains` בלי נעילה במקביל ל-`Add` → undefined behavior | נעילה על אותו `_gate` בקריאה ובספירה |
| 2 | 🔴 גבוה | מרוץ על שם היעד: `MakeUnique` + `Move(overwrite:false)` לא אטומיים → קובץ אובד | `MoveUnique` עם retry שמחשב את השם הפנוי הבא בהתנגשות |
| 3 | 🟠 בינוני | TOCTOU ב-dedup: `FindDuplicate` ו-`Register` בשתי נעילות נפרדות → כפילות מתפספסת | פרוטוקול claim/publish אטומי על hash התוכן |
| 4 | 🟠 בינוני | דוח ביטול היה קוד מת: `Parallel.ForEachAsync` זרק OCE לפני ה-`return` | עטיפה ב-`try/catch(OCE)` והחזרת דוח חלקי |
| 5 | 🟡 נמוך | כשלי ExifTool שקטים; `MetadataWritten` נספר גם בכישלון; `DrainErrors` קוד מת | חשיפת stderr של ExifTool ב-`ProcessingReport` |

**שיפור מרכזי — dedup חופשי ממרוצים:** `HashDeduplicator` שוכתב סביב hash תוכן
שמחושב **תוך כדי החילוץ** (`TakeoutArchiveReader.ExtractAsync` מזרים ל-SHA-256 בזמן
ה-copy). זה הפך את ההחלטה "כפילות?" לאטומית (`ConcurrentDictionary.GetOrAdd`), ביטל
כל גישת-קבצים מתוך ה-dedup (ולכן את כל מרוצי הקריאה/ההזזה), ופישט את הקוד
(quick-signature ו-buckets נמחקו) — וניטרלי בביצועים. כולל מסלול הצלה: אם ה-owner
של hash נכשל, הכפילות שומרת את עותקה במקום לאבד את הקובץ היחיד.

**בדיקות חדשות** (`tests/GPhotosTakeout.Tests/ConcurrencyTests.cs`, +4 → 57 סה"כ):
- 40 קבצים שונים שממופים לאותו יעד תחת מקביליות מקסימלית — אף קובץ לא אובד.
- 24 כפילויות זהות במקביל — נשמר עותק קנוני אחד בדיוק.
- claim של dedup ממקבילים רבים — בדיוק owner אחד.
- ביטול מחזיר `ProcessingReport` (`Cancelled=true`) במקום לזרוק חריגה.

**קבצים שעודכנו:** `Dedup/HashDeduplicator.cs`, `Archives/TakeoutArchiveReader.cs`
(+`ExtractResult`), `Pipeline/ResumeJournal.cs`, `Pipeline/ProcessingPipeline.cs`,
`Metadata/ExifToolPool.cs`, `tests/.../ConcurrencyTests.cs`, `README.md`.

**אימות:** `dotnet test` → 57/57 ✅ · `dotnet build` (App x64) → 0 warnings/errors ✅.

### 10.2 התקנת ExifTool והפעלת הממשק

- הורד **ExifTool 13.59** (Windows 64-bit) מ-SourceForge
  (`master.dl.sourceforge.net/project/exiftool/exiftool-13.59_64.zip` — ה-mirror-ים
  הראשיים החזירו 403 / דף interstitial; הקישור הישיר ב-`exiftool.org` החזיר 404).
- חולץ והותקן ב-build output:
  `src/GPhotosTakeout.App/bin/x64/Debug/net9.0-windows10.0.19041.0/Tools/` — הכולל
  `exiftool.exe` (שונה שמו מ-`exiftool(-k).exe`) **+ תיקיית `exiftool_files`** (חובה
  לריצה; ה-exe לבדו לא ירוץ בלעדיה). אומת `-ver` → 13.59.
- `Services/ExifToolLocator.cs` מאתר אותו אוטומטית (`Tools/exiftool.exe` יחסית ל-exe);
  אזהרת "ExifTool חסר" נעלמת וכתיבת ה-metadata פעילה.
- האפליקציה הופעלה (`GPhotosTakeout.App.exe`) ואומת שהתהליך חי עם חלון "מארגן Google
  Photos Takeout" (bootstrapper של WinUI3 Unpackaged עובד); החלון הובא לקדמת המסך.

⚠️ ההתקנה יושבת ב-build output בלבד. למצב קבוע יש להעתיק את `Tools/` ל-
`src/GPhotosTakeout.App/Tools/` ולהוסיף כלל
`<None Include="Tools\**" CopyToOutputDirectory="PreserveNewest" />` ל-`.csproj`
כדי שיועתק אוטומטית בכל בנייה.

---

## 11. סבב שיפור מקיף (תוכנית רב-שלבית)

מסמך התוכנית: `~/.claude/plans/jolly-honking-planet.md`. **החלטות:** הפצה = חנות
Microsoft (MSIX, full-trust desktop — היפוך החלטת ה-unpackaged), רב-לשוניות עברית+אנגלית,
והוספת CLI.

### 11.1 תשתית ו-Tooling
`git init`, `.gitignore`/`.gitattributes`/`.editorconfig`/`Directory.Build.props`
(אנלייזרים + `EnforceCodeStyleInBuild`), `coverlet`, ו-CI ב-GitHub Actions
(`.github/workflows/ci.yml`: build Core/CLI + test+coverage, build App x64).
**Core ו-CLI נבנים תחת `TreatWarningsAsErrors`.**

### 11.2 נכונות ועמידות (Core)
- `OptionsValidator` — ולידציה מקדימה (קלט/פלט/timezone/parallelism), errors+warnings.
- `ProcessingOptions.DryRun` — תכנון מלא ללא כתיבה.
- `FileOutcome` פר-קובץ + `ReportExporter` (JSON/CSV).
- `ProcessingProgress` — `ElapsedSeconds` → `ItemsPerSecond`/`EtaSeconds`.
- גבול ללולאות `MakeUnique`/`MoveUnique` + ניקוי `.part` יתומים בכל מסלול.
- **חוסן ExifTool:** timeout פר-קובץ (`ExifToolTimeout`); `ExifToolBatchWriter` עושה
  fault על timeout/יציאה לא צפויה; `ExifToolPool` מחליף תהליך מת ב-fresh ושומר את ה-stderr;
  חסימת גודל ל-stderr (64KB).

### 11.3 אפליקציית WinUI3
הצגת רשימת השגיאות בסיכום (היו מוסתרות) + ייצוא דוח (CSV/JSON), שמירת הגדרות
(`SettingsService` ל-JSON תחת `%LocalAppData%`), ולידציה מקדימה ב-UI עם InfoBar,
checkbox ל-dry-run, ETA/throughput חי, ו-DI ידני של שירותים ל-VM.

### 11.4 CLI (`gptakeout`)
פרויקט הרצה headless חדש, parser ללא תלויות, progress עם ETA, ביטול ב-Ctrl+C,
`--dry-run`, `--report`, exit codes. אומת end-to-end על Takeout סינתטי.

### 11.5 באגים שנמצאו ותוקנו בסבב הזה
- 🔴 `LongPath.Extended` השאיר `/` בנתיבי `\\?\` → נתיבים מוחלטים עם קו-נטוי "לא נמצאו"
  (הופיע בהרצת ה-CLI). תוקן: נרמול `/`→`\` לפני הוספת ה-prefix. + בדיקות.
- 🟠 `InvariantGlobalization=true` ב-CLI ביטל את מסד אזורי-הזמן (ICU) ושבר GPS→IANA. הוסר.
- אומת **שגוי**: `TimeZoneLookup.GetTimeZone(...).Result` אינו קריאה חוסמת — GeoTimeZone
  סינכרונית/לא-מקוונת; `.Result` היא property. אין שם deadlock.

### 11.6 בדיקות
מ-57 ל-**86** עוברות. נוספו: ולידציה, dry-run, ReportExporter, ETA, `LongPath`,
`TimezoneResolver`, `AlbumLinker`, `TakeoutArchiveReader` (כולל ZIP פגום ומולטי-ארכיון).

### 11.7 נותר (לא בוצע בסבב הזה)
לוגים מובנים (Microsoft.Extensions.Logging); i18n עברית+אנגלית (צריך אימות ידני של
החלפת שפה/RTL); מעבר ל-MSIX/Store + אימות ExifTool תחת זהות חבילה; פירוק עמוק יותר של
ה-ViewModel (DI container מלא).
