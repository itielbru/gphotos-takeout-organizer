using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GPhotosTakeout.App.Services;
using GPhotosTakeout.Core.Logging;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace GPhotosTakeout.App.ViewModels;

public enum WizardStep { Source = 0, Options = 1, Processing = 2, Summary = 3 }

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ISettingsService _settings;
    private CancellationTokenSource? _cts;
    private FileLoggerProvider? _logProvider;

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
        _fallbackTimeZone = s.FallbackTimeZone ?? ProcessingOptions.GetDefaultFallbackTimeZone();
        _dryRun = s.DryRun;

        var lang = Localization.FromCode(s.Language);
        _languageIndex = lang == AppLanguage.Hebrew ? 0 : 1;
        _s = Localization.For(lang);
        _flowDirection = Localization.FlowFor(lang);

        ZipFiles.CollectionChanged += OnZipFilesChanged;
        _ = CheckExifToolUpdateAsync();
    }

    private async Task CheckExifToolUpdateAsync()
    {
        try
        {
            ExifToolUpdateAvailable = await ExifToolInstaller.IsUpdateAvailableAsync().ConfigureAwait(false);
        }
        catch { /* non-critical: if check fails, no upgrade button shown */ }
    }

    private void OnZipFilesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasZips));
        OnPropertyChanged(nameof(HasNoZips));
        GoNextCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<string> ZipFiles { get; } = new();
    public ObservableCollection<string> ReportErrors { get; } = new();

    // Localization: bound OneWay so switching language updates the whole UI live.
    [ObservableProperty] private AppStrings _s = Localization.For(AppLanguage.Hebrew);
    [ObservableProperty] private FlowDirection _flowDirection = FlowDirection.RightToLeft;
    [ObservableProperty] private int _languageIndex;   // 0 = Hebrew, 1 = English

    partial void OnLanguageIndexChanged(int value)
    {
        var lang = value == 0 ? AppLanguage.Hebrew : AppLanguage.English;
        S = Localization.For(lang);
        FlowDirection = Localization.FlowFor(lang);
        OnPropertyChanged(nameof(SummaryText));
        PersistSettings();
    }

    private AppLanguage CurrentLanguage => LanguageIndex == 0 ? AppLanguage.Hebrew : AppLanguage.English;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceVisibility))]
    [NotifyPropertyChangedFor(nameof(OptionsVisibility))]
    [NotifyPropertyChangedFor(nameof(ProcessingVisibility))]
    [NotifyPropertyChangedFor(nameof(SummaryVisibility))]
    [NotifyPropertyChangedFor(nameof(StepIndex))]
    [NotifyPropertyChangedFor(nameof(Step2Opacity))]
    [NotifyPropertyChangedFor(nameof(Step3Opacity))]
    [NotifyPropertyChangedFor(nameof(Step4Opacity))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private WizardStep _step = WizardStep.Source;
    [ObservableProperty] private string? _outputDirectory;
    [ObservableProperty] private string? _exifToolPath;

    // Options (indexes map to the enums).
    [ObservableProperty] private int _outputStructureIndex;   // 0 year/month, 1 albums, 2 flat
    [ObservableProperty] private int _albumStrategyIndex;     // 0 shortcut, 1 duplicate, 2 json, 3 nothing
    [ObservableProperty] private int _duplicateHandlingIndex; // 0 keep best, 1 keep all
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTimezoneError))]
    private string? _fallbackTimeZone = ProcessingOptions.GetDefaultFallbackTimeZone();
    [ObservableProperty] private bool _dryRun;

    // Live timezone validation for Step 2 (empty is allowed — it's optional).
    public bool HasTimezoneError =>
        !string.IsNullOrWhiteSpace(FallbackTimeZone) && !IsValidTimeZone(FallbackTimeZone);

    private static bool IsValidTimeZone(string tz)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(tz); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    // Validation.
    [ObservableProperty] private string? _validationMessage;

    // Progress.
    [ObservableProperty] private double _progressFraction;
    [ObservableProperty] private bool _progressIndeterminate;
    [ObservableProperty] private string _phase = "";
    [ObservableProperty] private string? _currentFile;
    [ObservableProperty] private string? _etaText;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLiveErrors))]
    [NotifyPropertyChangedFor(nameof(LiveErrorText))]
    private int _errorCount;
    [ObservableProperty] private bool _isRunning;

    public Visibility HasLiveErrors => Visible(ErrorCount > 0);
    public string LiveErrorText => $"{ErrorCount} {S.LblErrors}";

    // ExifTool one-click install / upgrade (Step 2).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotInstallingExifTool))]
    [NotifyCanExecuteChangedFor(nameof(InstallExifToolCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpgradeExifToolCommand))]
    private bool _isInstallingExifTool;
    [ObservableProperty] private double _exifInstallProgress;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstallError))]
    private string? _exifInstallError;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpgradeExifTool))]
    [NotifyCanExecuteChangedFor(nameof(UpgradeExifToolCommand))]
    private bool _exifToolUpdateAvailable;
    public bool IsNotInstallingExifTool => !IsInstallingExifTool;
    public Visibility HasInstallError => Visible(!string.IsNullOrEmpty(ExifInstallError));
    public bool CanUpgradeExifTool => ExifToolUpdateAvailable && !IsInstallingExifTool;

    // Summary.
    [ObservableProperty] private ProcessingReport? _report;
    [ObservableProperty] private string? _lastLogPath;

    public bool HasLog => !string.IsNullOrEmpty(LastLogPath);
    partial void OnLastLogPathChanged(string? value) => OnPropertyChanged(nameof(HasLog));

    public bool MetadataAvailable => ExifToolPath is not null;
    public bool MetadataMissing => ExifToolPath is null;

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);
    public bool HasErrors => Report is { Errors: > 0 };
    public bool CanExportReport => Report is not null;

    partial void OnValidationMessageChanged(string? value) => OnPropertyChanged(nameof(HasValidationError));

    // Status line shown above the result cards (the per-stat counts moved to cards).
    public string SummaryText
    {
        get
        {
            if (Report is not { } r)
                return S.SummaryNoData;
            if (r.Cancelled)
                return S.SummaryCancelled;
            return r.DryRun ? S.SummaryDryRun : S.SummaryDone;
        }
    }

    // Result-card values (string-typed for direct TextBlock binding).
    public string StatTotal => (Report?.TotalMedia ?? 0).ToString(CultureInfo.InvariantCulture);
    public string StatMatched => (Report?.Matched ?? 0).ToString(CultureInfo.InvariantCulture);
    public string StatDuplicates => (Report?.Duplicates ?? 0).ToString(CultureInfo.InvariantCulture);
    public string StatMetadata => (Report?.MetadataWritten ?? 0).ToString(CultureInfo.InvariantCulture);
    public string StatErrors => (Report?.Errors ?? 0).ToString(CultureInfo.InvariantCulture);

    partial void OnReportChanged(ProcessingReport? value)
    {
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(CanExportReport));
        OnPropertyChanged(nameof(StatTotal));
        OnPropertyChanged(nameof(StatMatched));
        OnPropertyChanged(nameof(StatDuplicates));
        OnPropertyChanged(nameof(StatMetadata));
        OnPropertyChanged(nameof(StatErrors));

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

    public Visibility HasZips => Visible(ZipFiles.Count > 0);
    public Visibility HasNoZips => Visible(ZipFiles.Count == 0);

    private static Visibility Visible(bool b) => b ? Visibility.Visible : Visibility.Collapsed;

    // Stepper header state. Step N lights up once it has been reached; dimmed otherwise.
    public int StepIndex => (int)Step;
    public double Step1Opacity => 1;
    public double Step2Opacity => StepIndex >= 1 ? 1 : 0.35;
    public double Step3Opacity => StepIndex >= 2 ? 1 : 0.35;
    public double Step4Opacity => StepIndex >= 3 ? 1 : 0.35;

    public void AddZips(IEnumerable<string> paths)
    {
        foreach (var p in paths)
            if (!ZipFiles.Contains(p))
                ZipFiles.Add(p);
        GoNextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveZip(string path) => ZipFiles.Remove(path);

    private bool CanInstallExifTool() => !IsInstallingExifTool;

    [RelayCommand(CanExecute = nameof(CanInstallExifTool))]
    private async Task InstallExifToolAsync()
    {
        IsInstallingExifTool = true;
        ExifInstallError = null;
        ExifInstallProgress = 0;
        try
        {
            var progress = new Progress<double>(p => _dispatcher.TryEnqueue(() => ExifInstallProgress = p));
            var path = await ExifToolInstaller.InstallAsync(progress);
            ExifToolPath = path;
            ExifToolUpdateAvailable = false;
            OnPropertyChanged(nameof(MetadataAvailable));
            OnPropertyChanged(nameof(MetadataMissing));
        }
        catch (Exception ex)
        {
            ExifInstallError = ex.Message;
        }
        finally
        {
            IsInstallingExifTool = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpgradeExifTool))]
    private Task UpgradeExifToolAsync() => InstallExifToolAsync();

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
        Language = Localization.ToCode(CurrentLanguage),
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
        ProgressIndeterminate = true;
        EtaText = null;
        StartCommand.NotifyCanExecuteChanged();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var options = BuildOptions();

        var progress = new Progress<ProcessingProgress>(p => _dispatcher.TryEnqueue(() =>
        {
            Phase = TranslatePhase(p.Phase);
            ProgressFraction = p.Fraction;
            ProgressIndeterminate = p.Phase != "Processing";
            CurrentFile = p.CurrentFile;
            ErrorCount = p.Errors;
            EtaText = FormatEta(p);
        }));

        var logger = CreateRunLogger();

        try
        {
            var pipeline = new ProcessingPipeline(ExifToolPath, logger);
            Report = await Task.Run(() => pipeline.RunAsync(options, progress, _cts!.Token));
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
                ErrorMessages = new[] { S.UnexpectedError + ex.Message },
            };
        }
        finally
        {
            IsRunning = false;
            ProgressIndeterminate = false;
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

    private ILogger CreateRunLogger()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GPhotosTakeout", "logs", $"run-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        _logProvider?.Dispose();
        _logProvider = new FileLoggerProvider(path);
        LastLogPath = path;
        return _logProvider.CreateLogger("Pipeline");
    }

    private string FormatEta(ProcessingProgress p)
    {
        if (p.Phase != "Processing")
            return "";
        var rate = $"{p.ItemsPerSecond:F1} {S.FilesPerSec}";
        if (p.EtaSeconds is { } secs)
        {
            var eta = TimeSpan.FromSeconds(secs).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            return $"{rate} · {S.RemainingPrefix}{eta}";
        }
        return rate;
    }

    private string TranslatePhase(string phase) => phase switch
    {
        "Indexing" => S.PhaseIndexing,
        "Matching" => S.PhaseMatching,
        "Processing" => S.PhaseProcessing,
        _ => phase,
    };

    public void Dispose()
    {
        _cts?.Dispose();
        _logProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}
