using System;
using System.Diagnostics;
using System.Linq;
using GPhotosTakeout.App.Services;
using GPhotosTakeout.App.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GPhotosTakeout.App;

public sealed partial class MainWindow : Window
{
    private static readonly string[] CsvExt = [".csv"];
    private static readonly string[] JsonExt = [".json"];

    public MainViewModel Vm { get; }

    public MainWindow()
    {
        InitializeComponent();
        Vm = new MainViewModel(DispatcherQueue, new SettingsService());
        Title = "מארגן Google Photos Takeout";
    }

    private nint Hwnd => WindowNative.GetWindowHandle(this);

    private async void OnAddZips(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.FileTypeFilter.Add(".zip");
        picker.SuggestedStartLocation = PickerLocationId.Downloads;

        var files = await picker.PickMultipleFilesAsync();
        if (files is { Count: > 0 })
            Vm.AddZips(files.Select(f => f.Path));
    }

    private async void OnChooseOutput(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.Desktop;

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            Vm.SetOutput(folder.Path);
    }

    private void OnOpenOutput(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Vm.OutputDirectory))
            return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Vm.OutputDirectory,
                UseShellExecute = true,
            });
        }
        catch
        {
            // best effort
        }
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Vm.LastLogPath))
            return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = Vm.LastLogPath, UseShellExecute = true });
        }
        catch
        {
            // best effort
        }
    }

    private async void OnExportReport(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.SuggestedFileName = "gphotos-report";
        picker.FileTypeChoices.Add("CSV", CsvExt);
        picker.FileTypeChoices.Add("JSON", JsonExt);

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
            await Vm.ExportReportAsync(file.Path);
    }
}
