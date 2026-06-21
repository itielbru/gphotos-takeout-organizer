using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GPhotosTakeout.App.Services;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace GPhotosTakeout.App.ViewModels;

public enum WizardStep { Source = 0, Options = 1, Processing = 2, Summary = 3 }

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ISettingsService _settings;
    private CancellationTokenSource? _cts;

    public MainViewModel(DispatcherQueue dispatcher, ISettingsService settings)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        ExifToolPath = ExifToolLocator.Find();

        var s = _settings.Load();
        _outputDirectory = s.OutputDirectory;
        _outputStructureIndex = s.OutputStructureIndex;
        _albumStrategyIndex = s.AlbumStrategyIndex;
        _duplicateHandlingIndex = s.DuplicateHandlingIndex;
        _fallbackTimeZone = s.FallbackTimeZone ?? "Asia/Jerusalem";
        _dryRun = s.DryRun;
    }

    public ObservableCollection<string> ZipFiles { get; } = new();
    public ObservableCollection<string> ReportErrors { get; } = new();

    [ObservableProperty] private WizardStep _step = WizardStep.Source;
    [ObservableProperty] private string? _outputDirectory;
    [ObservableProperty] private string? _exifToolPath;

    // Options (indexes map to the enums).
    [ObservableProperty] private int _outputStructureIndex;   // 0 year/month, 1 albums, 2 flat
    [ObservableProperty] private int _albumStrategyIndex;     // 0 shortcut, 1 duplicate, 2 json, 3 nothing
    [ObservableProperty] private int _duplicateHandlingIndex; // 0 keep best, 1 keep all
    [ObservableProperty] private string? _fallbackTimeZone = "Asia/Jerusalem";
    [ObservableProperty] private bool _dryRun;

    // Validation.
    [ObservableProperty] private string? _validationMessage;

    // Progress.
    [ObservableProperty] private double _progressFraction;
    [ObservableProperty] private string _phase = "";
    [ObservableProperty] private string? _currentFile;
    [ObservableProperty] private string? _etaText;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private bool _isRunning;

    // Summary.
    [ObservableProperty] private ProcessingReport? _report;

    public bool MetadataAvailable => ExifToolPath is not null;
    public bool MetadataMissing => ExifToolPath is null;

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);
    public bool HasErrors => Report is { Errors: > 0 };
    public bool CanExportReport => Report is not null;

    partial void OnValidationMessageChanged(string? value) => OnPropertyChanged(nameof(HasValidationError));

    public string SummaryText
    {
        get
        {
            if (Report is not { } r)
                return "אין נתונים.";
            var lines = new[]
            {
                r.DryRun ? "תצוגה מקדימה (Dry-run) — לא נכתב דבר לדיסק." : "העיבוד הושלם.",
                $"סך הכל קבצים: {r.TotalMedia}",
                $"הותאמו למטא-דאטה: {r.Matched}",
                $"ללא JSON: {r.Unmatched}",
                $"כפילויות שהוסרו: {r.Duplicates}",
                $"מטא-דאטה נכתב: {r.MetadataWritten}",
                $"תיקיות מיוחדות: {r.SpecialFolderItems}",
                $"שגיאות: {r.Errors}",
                r.Cancelled ? "⚠ העיבוד בוטל (תוצאה חלקית)." : "",
            };
            return string.Join(Environment.NewLine, lines.Where(l => l.Length > 0));
        }
    }

    partial void OnReportChanged(ProcessingReport? value)
    {
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(CanExportReport));
        ExportReportCommand.NotifyCanExecuteChanged();

        ReportErrors.Clear();
        if (value is not null)
            foreach (var msg in value.ErrorMessages)
                ReportErrors.Add(msg);
    }

    // Step visibility helpers. Visibility-typed (not bool+converter) because a
    // converter inside a compiled x:Bind on a Window root generates invalid code
    // (Window is not a FrameworkElement).
    public Visibility SourceVisibility => Visible(Step == WizardStep.Source);
    public Visibility OptionsVisibility => Visible(Step == WizardStep.Options);
    public Visibility ProcessingVisibility => Visible(Step == WizardStep.Processing);
    public Visibility SummaryVisibility => Visible(Step == WizardStep.Summary);

    private static Visibility Visible(bool b) => b ? Visibility.Visible : Visibility.Collapsed;

    partial void OnStepChanged(WizardStep value)
    {
        OnPropertyChanged(nameof(SourceVisibility));
        OnPropertyChanged(nameof(OptionsVisibility));
        OnPropertyChanged(nameof(ProcessingVisibility));
        OnPropertyChanged(nameof(SummaryVisibility));
        StartCommand.NotifyCanExecuteChanged();
        GoNextCommand.NotifyCanExecuteChanged();
    }

    public void AddZips(IEnumerable<string> paths)
    {
        foreach (var p in paths)
            if (!ZipFiles.Contains(p))
                ZipFiles.Add(p);
        GoNextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveZip(string path) => ZipFiles.Remove(path);

    public void SetOutput(string path)
    {
        OutputDirectory = path;
        ValidationMessage = null;
        StartCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoNext() => Step != WizardStep.Source || ZipFiles.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void GoNext()
    {
        if (Step == WizardStep.Source)
        {
            Step = WizardStep.Options;
        }
        else if (Step == WizardStep.Options)
        {
            // Validate before letting the user reach the run screen.
            if (!Validate())
                return;
            Step = WizardStep.Processing;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (Step == WizardStep.Options) Step = WizardStep.Source;
        else if (Step == WizardStep.Processing && !IsRunning) Step = WizardStep.Options;
    }

    private bool Validate()
    {
        var result = OptionsValidator.Validate(BuildOptions(), MetadataAvailable);
        ValidationMessage = result.IsValid ? null : string.Join(Environment.NewLine, result.Errors);
        return result.IsValid;
    }

    private ProcessingOptions BuildOptions() => new()
    {
        InputZipPaths = ZipFiles.ToList(),
        OutputDirectory = OutputDirectory ?? "",
        OutputStructure = (OutputStructure)OutputStructureIndex,
        AlbumStrategy = (AlbumStrategy)AlbumStrategyIndex,
        DuplicateHandling = (DuplicateHandling)DuplicateHandlingIndex,
        FallbackTimeZone = string.IsNullOrWhiteSpace(FallbackTimeZone) ? null : FallbackTimeZone,
        WriteMetadata = MetadataAvailable,
        DryRun = DryRun,
    };

    private void PersistSettings() => _settings.Save(new AppSettings
    {
        OutputDirectory = OutputDirectory,
        OutputStructureIndex = OutputStructureIndex,
        AlbumStrategyIndex = AlbumStrategyIndex,
        DuplicateHandlingIndex = DuplicateHandlingIndex,
        FallbackTimeZone = FallbackTimeZone,
        DryRun = DryRun,
    });

    private bool CanStart() => Step == WizardStep.Processing && !IsRunning
                               && ZipFiles.Count > 0 && !string.IsNullOrWhiteSpace(OutputDirectory);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!Validate())
            return;

        PersistSettings();

        IsRunning = true;
        ErrorCount = 0;
        ProgressFraction = 0;
        EtaText = null;
        StartCommand.NotifyCanExecuteChanged();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var options = BuildOptions();

        var progress = new Progress<ProcessingProgress>(p => _dispatcher.TryEnqueue(() =>
        {
            Phase = TranslatePhase(p.Phase);
            ProgressFraction = p.Fraction;
            CurrentFile = p.CurrentFile;
            ErrorCount = p.Errors;
            EtaText = FormatEta(p);
        }));

        try
        {
            var pipeline = new ProcessingPipeline(ExifToolPath);
            Report = await Task.Run(() => pipeline.RunAsync(options, progress, _cts.Token));
        }
        catch (OperationCanceledException)
        {
            // The pipeline returns a partial report on cancel; this only fires if the
            // run faulted before producing one.
        }
        catch (Exception ex)
        {
            Report = new ProcessingReport
            {
                Errors = 1,
                ErrorMessages = new[] { "שגיאה בלתי צפויה: " + ex.Message },
            };
        }
        finally
        {
            IsRunning = false;
            StartCommand.NotifyCanExecuteChanged();
            Step = WizardStep.Summary;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void Restart()
    {
        Report = null;
        ProgressFraction = 0;
        CurrentFile = null;
        EtaText = null;
        Phase = "";
        ValidationMessage = null;
        Step = WizardStep.Source;
    }

    [RelayCommand(CanExecute = nameof(CanExportReport))]
    private Task ExportReport() => Task.CompletedTask; // actual export is driven from the window (needs a save picker)

    /// <summary>Writes the current report to the given path (.csv → CSV, else JSON).</summary>
    public async Task ExportReportAsync(string path)
    {
        if (Report is not { } report)
            return;
        if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            await ReportExporter.WriteCsvAsync(report, path);
        else
            await ReportExporter.WriteJsonAsync(report, path);
    }

    private static string FormatEta(ProcessingProgress p)
    {
        if (p.Phase != "Processing")
            return "";
        var rate = $"{p.ItemsPerSecond:F1} קבצים/שנייה";
        if (p.EtaSeconds is { } secs)
        {
            var eta = TimeSpan.FromSeconds(secs).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            return $"{rate} · נותר ~{eta}";
        }
        return rate;
    }

    private static string TranslatePhase(string phase) => phase switch
    {
        "Indexing" => "מאנדקס קבצים…",
        "Matching" => "מתאים מטא-דאטה…",
        "Processing" => "מעבד תמונות…",
        _ => phase,
    };

    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
