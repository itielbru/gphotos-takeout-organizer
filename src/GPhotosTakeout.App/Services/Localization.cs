using System;
using Microsoft.UI.Xaml;

namespace GPhotosTakeout.App.Services;

public enum AppLanguage { Hebrew, English }

/// <summary>
/// All user-visible UI strings for one language. Properties are <c>required</c> so the
/// compiler guarantees every language fills every string — adding a new label that one
/// language forgets won't build. Bindings are code-based (not .resw) so language can be
/// switched live and the set is unit-testable.
/// </summary>
public sealed class AppStrings
{
    public required string AppTitle { get; init; }
    public required string AppSubtitle { get; init; }
    public required string LanguageLabel { get; init; }

    public required string Step1Title { get; init; }
    public required string Step1Hint { get; init; }
    public required string AddZips { get; init; }
    public required string Continue { get; init; }
    public required string Back { get; init; }

    public required string Step2Title { get; init; }
    public required string OutputStructureLabel { get; init; }
    public required string StructureYearMonth { get; init; }
    public required string StructureAlbums { get; init; }
    public required string StructureFlat { get; init; }
    public required string AlbumLabel { get; init; }
    public required string AlbumShortcut { get; init; }
    public required string AlbumDuplicate { get; init; }
    public required string AlbumJson { get; init; }
    public required string AlbumNothing { get; init; }
    public required string DuplicatesLabel { get; init; }
    public required string DupKeepBest { get; init; }
    public required string DupKeepAll { get; init; }
    public required string TimezoneLabel { get; init; }
    public required string TimezonePlaceholder { get; init; }
    public required string OutputLabel { get; init; }
    public required string ChooseFolder { get; init; }
    public required string DryRunLabel { get; init; }
    public required string ExifFoundTitle { get; init; }
    public required string ExifFoundMsg { get; init; }
    public required string ExifMissingTitle { get; init; }
    public required string ExifMissingMsg { get; init; }
    public required string ValidationTitle { get; init; }

    public required string Step3Title { get; init; }
    public required string Start { get; init; }
    public required string Cancel { get; init; }
    public required string PhaseIndexing { get; init; }
    public required string PhaseMatching { get; init; }
    public required string PhaseProcessing { get; init; }
    public required string FilesPerSec { get; init; }
    public required string RemainingPrefix { get; init; }

    public required string SummaryTitle { get; init; }
    public required string OpenOutput { get; init; }
    public required string ExportReport { get; init; }
    public required string OpenLog { get; init; }
    public required string Restart { get; init; }
    public required string ErrorsTitle { get; init; }
    public required string SummaryNoData { get; init; }
    public required string SummaryDone { get; init; }
    public required string SummaryDryRun { get; init; }
    public required string SummaryCancelled { get; init; }
    public required string LblTotal { get; init; }
    public required string LblMatched { get; init; }
    public required string LblUnmatched { get; init; }
    public required string LblDuplicates { get; init; }
    public required string LblMetadata { get; init; }
    public required string LblSpecial { get; init; }
    public required string LblErrors { get; init; }

    // Accessibility names, crash reporting, and the per-row remove button.
    public required string RemoveFile { get; init; }
    public required string ProgressLabel { get; init; }
    public required string CopyErrors { get; init; }
    public required string UnexpectedError { get; init; }

    // Stepper header (short labels).
    public required string StepperSource { get; init; }
    public required string StepperOptions { get; init; }
    public required string StepperRun { get; init; }
    public required string StepperDone { get; init; }
    public required string DropHint { get; init; }
    public required string TimezoneInvalid { get; init; }
    public required string InstallExifTool { get; init; }
}

/// <summary>Provides the string table and flow direction for a language.</summary>
public static class Localization
{
    public static FlowDirection FlowFor(AppLanguage lang) =>
        lang == AppLanguage.Hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Maps a saved code ("he"/"en"/null) or the OS culture to a language.</summary>
    public static AppLanguage FromCode(string? code)
    {
        if (string.Equals(code, "he", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Hebrew;
        if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)) return AppLanguage.English;
        var os = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return string.Equals(os, "he", StringComparison.OrdinalIgnoreCase) ? AppLanguage.Hebrew : AppLanguage.English;
    }

    public static string ToCode(AppLanguage lang) => lang == AppLanguage.Hebrew ? "he" : "en";

    public static AppStrings For(AppLanguage lang) => lang == AppLanguage.Hebrew ? Hebrew : English;

    private static readonly AppStrings Hebrew = new()
    {
        AppTitle = "מארגן Google Photos Takeout",
        AppSubtitle = "מאחד מטא-דאטה, מתקן תאריכים ומסדר את ספריית התמונות שלך",
        LanguageLabel = "שפה / Language",
        Step1Title = "שלב 1 — בחירת קבצים",
        Step1Hint = "הוסף את קובצי ה-ZIP של ה-Takeout (אפשר כמה חלקים יחד).",
        AddZips = "הוסף קבצי ZIP",
        Continue = "המשך",
        Back = "חזרה",
        Step2Title = "שלב 2 — הגדרות עיבוד",
        OutputStructureLabel = "מבנה תיקיות הפלט",
        StructureYearMonth = "לפי שנה/חודש (מומלץ)",
        StructureAlbums = "לפי אלבומים",
        StructureFlat = "תיקייה אחת (שטוח)",
        AlbumLabel = "טיפול באלבומים",
        AlbumShortcut = "קיצורי דרך (חוסך מקום)",
        AlbumDuplicate = "העתקה כפולה",
        AlbumJson = "קובץ JSON של אלבומים",
        AlbumNothing = "להתעלם מאלבומים",
        DuplicatesLabel = "כפילויות",
        DupKeepBest = "לשמור עותק אחד (מומלץ)",
        DupKeepAll = "לשמור את כל העותקים",
        TimezoneLabel = "אזור זמן ברירת מחדל (כשאין GPS)",
        TimezonePlaceholder = "לדוגמה: Asia/Jerusalem",
        OutputLabel = "תיקיית פלט",
        ChooseFolder = "בחר תיקייה",
        DryRunLabel = "תצוגה מקדימה בלבד (Dry-run) — תכנון וחישוב ללא כתיבה לדיסק",
        ExifFoundTitle = "ExifTool זוהה",
        ExifFoundMsg = "מטא-דאטה ייכתב ישירות לקבצים.",
        ExifMissingTitle = "ExifTool לא נמצא",
        ExifMissingMsg = "הקבצים יאורגנו ויתוארכו, אך מטא-דאטה לא ייכתב. הנח את exiftool.exe בתיקיית Tools.",
        ValidationTitle = "לא ניתן להמשיך",
        Step3Title = "שלב 3 — עיבוד",
        Start = "התחל עיבוד",
        Cancel = "ביטול",
        PhaseIndexing = "מאנדקס קבצים…",
        PhaseMatching = "מתאים מטא-דאטה…",
        PhaseProcessing = "מעבד תמונות…",
        FilesPerSec = "קבצים/שנייה",
        RemainingPrefix = "נותר ~",
        SummaryTitle = "סיכום",
        OpenOutput = "פתח את תיקיית הפלט",
        ExportReport = "ייצא דוח (CSV/JSON)",
        OpenLog = "פתח קובץ לוג",
        Restart = "התחל מחדש",
        ErrorsTitle = "היו שגיאות בעיבוד",
        SummaryNoData = "אין נתונים.",
        SummaryDone = "העיבוד הושלם.",
        SummaryDryRun = "תצוגה מקדימה (Dry-run) — לא נכתב דבר לדיסק.",
        SummaryCancelled = "⚠ העיבוד בוטל (תוצאה חלקית).",
        LblTotal = "סך הכל קבצים",
        LblMatched = "הותאמו למטא-דאטה",
        LblUnmatched = "ללא JSON",
        LblDuplicates = "כפילויות שהוסרו",
        LblMetadata = "מטא-דאטה נכתב",
        LblSpecial = "תיקיות מיוחדות",
        LblErrors = "שגיאות",
        RemoveFile = "הסר קובץ",
        ProgressLabel = "התקדמות העיבוד",
        CopyErrors = "העתק שגיאות",
        UnexpectedError = "שגיאה בלתי צפויה: ",
        StepperSource = "מקור",
        StepperOptions = "הגדרות",
        StepperRun = "עיבוד",
        StepperDone = "סיום",
        DropHint = "גרור לכאן קובצי ZIP או לחץ \"הוסף קבצי ZIP\"",
        TimezoneInvalid = "אזור זמן לא תקין (לדוגמה: Asia/Jerusalem)",
        InstallExifTool = "הורד והתקן ExifTool",
    };

    private static readonly AppStrings English = new()
    {
        AppTitle = "Google Photos Takeout Organizer",
        AppSubtitle = "Merges metadata, fixes dates, and tidies up your photo library",
        LanguageLabel = "Language / שפה",
        Step1Title = "Step 1 — Select files",
        Step1Hint = "Add the Takeout ZIP files (multiple parts are fine).",
        AddZips = "Add ZIP files",
        Continue = "Continue",
        Back = "Back",
        Step2Title = "Step 2 — Processing options",
        OutputStructureLabel = "Output folder structure",
        StructureYearMonth = "By year/month (recommended)",
        StructureAlbums = "By albums",
        StructureFlat = "Single folder (flat)",
        AlbumLabel = "Album handling",
        AlbumShortcut = "Shortcuts (saves space)",
        AlbumDuplicate = "Duplicate copies",
        AlbumJson = "Albums JSON manifest",
        AlbumNothing = "Ignore albums",
        DuplicatesLabel = "Duplicates",
        DupKeepBest = "Keep one copy (recommended)",
        DupKeepAll = "Keep all copies",
        TimezoneLabel = "Default timezone (when no GPS)",
        TimezonePlaceholder = "e.g. Asia/Jerusalem",
        OutputLabel = "Output directory",
        ChooseFolder = "Choose folder",
        DryRunLabel = "Preview only (dry run) — plan & compute without writing to disk",
        ExifFoundTitle = "ExifTool detected",
        ExifFoundMsg = "Metadata will be written directly into the files.",
        ExifMissingTitle = "ExifTool not found",
        ExifMissingMsg = "Files will be organized and dated, but metadata won't be written. Place exiftool.exe in the Tools folder.",
        ValidationTitle = "Cannot continue",
        Step3Title = "Step 3 — Processing",
        Start = "Start processing",
        Cancel = "Cancel",
        PhaseIndexing = "Indexing files…",
        PhaseMatching = "Matching metadata…",
        PhaseProcessing = "Processing photos…",
        FilesPerSec = "files/sec",
        RemainingPrefix = "~",
        SummaryTitle = "Summary",
        OpenOutput = "Open output folder",
        ExportReport = "Export report (CSV/JSON)",
        OpenLog = "Open log file",
        Restart = "Start over",
        ErrorsTitle = "There were processing errors",
        SummaryNoData = "No data.",
        SummaryDone = "Processing complete.",
        SummaryDryRun = "Preview (dry run) — nothing was written to disk.",
        SummaryCancelled = "⚠ Processing cancelled (partial result).",
        LblTotal = "Total files",
        LblMatched = "Matched to metadata",
        LblUnmatched = "Without JSON",
        LblDuplicates = "Duplicates removed",
        LblMetadata = "Metadata written",
        LblSpecial = "Special folders",
        LblErrors = "Errors",
        RemoveFile = "Remove file",
        ProgressLabel = "Processing progress",
        CopyErrors = "Copy errors",
        UnexpectedError = "Unexpected error: ",
        StepperSource = "Source",
        StepperOptions = "Options",
        StepperRun = "Run",
        StepperDone = "Done",
        DropHint = "Drag ZIP files here, or click \"Add ZIP files\"",
        TimezoneInvalid = "Invalid timezone (e.g. Asia/Jerusalem)",
        InstallExifTool = "Download & install ExifTool",
    };
}
