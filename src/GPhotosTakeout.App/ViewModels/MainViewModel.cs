using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _cts;

    public MainViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        ExifToolPath = ExifToolLocator.Find();
    }

    public ObservableCollection<string> ZipFiles { get; } = new();

    [ObservableProperty] private WizardStep _step = WizardStep.Source;
    [ObservableProperty] private string? _outputDirectory;
    [ObservableProperty] private string? _exifToolPath;

    // Options (indexes map to the enums).
    [ObservableProperty] private int _outputStructureIndex;   // 0 year/month, 1 albums, 2 flat
    [ObservableProperty] private int _albumStrategyIndex;     // 0 shortcut, 1 duplicate, 2 json, 3 nothing
    [ObservableProperty] private int _duplicateHandlingIndex; // 0 keep best, 1 keep all
    [ObservableProperty] private string? _fallbackTimeZone = "Asia/Jerusalem";

    // Progress.
    [ObservableProperty] private double _progressFraction;
    [ObservableProperty] private string _phase = "";
    [ObservableProperty] private string? _currentFile;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private bool _isRunning;

    // Summary.
    [ObservableProperty] private ProcessingReport? _report;

    public bool MetadataAvailable => ExifToolPath is not null;
    public bool MetadataMissing => ExifToolPath is null;

    public string SummaryText
    {
        get
        {
            if (Report is not { } r)
                return "אין נתונים.";
            var lines = new[]
            {
                $"סך הכל קבצים: {r.TotalMedia}",
                $"הותאמו למטא-דאטה: {r.Matched}",
                $"ללא JSON: {r.Unmatched}",
                $"כפילויות שהוסרו: {r.Duplicates}",
                $"מטא-דאטה נכתב: {r.MetadataWritten}",
                $"תיקיות מיוחדות: {r.SpecialFolderItems}",
                $"שגיאות: {r.Errors}",
                r.Cancelled ? "העיבוד בוטל." : "העיבוד הושלם.",
            };
            return string.Join(Environment.NewLine, lines);
        }
    }

    partial void OnReportChanged(ProcessingReport? value) => OnPropertyChanged(nameof(SummaryText));

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
        StartCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoNext() => Step != WizardStep.Source || ZipFiles.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void GoNext()
    {
        if (Step == WizardStep.Source) Step = WizardStep.Options;
        else if (Step == WizardStep.Options) Step = WizardStep.Processing;
    }

    [RelayCommand]
    private void GoBack()
    {
        if (Step == WizardStep.Options) Step = WizardStep.Source;
        else if (Step == WizardStep.Processing && !IsRunning) Step = WizardStep.Options;
    }

    private bool CanStart() => Step == WizardStep.Processing && !IsRunning
                               && ZipFiles.Count > 0 && !string.IsNullOrWhiteSpace(OutputDirectory);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsRunning = true;
        ErrorCount = 0;
        ProgressFraction = 0;
        StartCommand.NotifyCanExecuteChanged();
        _cts = new CancellationTokenSource();

        var options = new ProcessingOptions
        {
            InputZipPaths = ZipFiles.ToList(),
            OutputDirectory = OutputDirectory!,
            OutputStructure = (OutputStructure)OutputStructureIndex,
            AlbumStrategy = (AlbumStrategy)AlbumStrategyIndex,
            DuplicateHandling = (DuplicateHandling)DuplicateHandlingIndex,
            FallbackTimeZone = string.IsNullOrWhiteSpace(FallbackTimeZone) ? null : FallbackTimeZone,
            WriteMetadata = MetadataAvailable,
        };

        var progress = new Progress<ProcessingProgress>(p => _dispatcher.TryEnqueue(() =>
        {
            Phase = TranslatePhase(p.Phase);
            ProgressFraction = p.Fraction;
            CurrentFile = p.CurrentFile;
            ErrorCount = p.Errors;
        }));

        try
        {
            var pipeline = new ProcessingPipeline(ExifToolPath);
            var report = await Task.Run(() => pipeline.RunAsync(options, progress, _cts.Token));
            Report = report;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is reflected in the report path below on next run.
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
        Phase = "";
        Step = WizardStep.Source;
    }

    private static string TranslatePhase(string phase) => phase switch
    {
        "Indexing" => "מאנדקס קבצים…",
        "Matching" => "מתאים מטא-דאטה…",
        "Processing" => "מעבד תמונות…",
        _ => phase,
    };
}
