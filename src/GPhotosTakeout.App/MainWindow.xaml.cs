using System;
using System.Diagnostics;
using System.Linq;
using GPhotosTakeout.App.Services;
using GPhotosTakeout.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
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
        Root.DataContext = Vm;
        Title = Vm.S.AppTitle;

        // Mica backdrop — transparent glass effect on Windows 11; graceful no-op on Win10.
        SystemBackdrop = new MicaBackdrop();
    }

    private nint Hwnd => WindowNative.GetWindowHandle(this);

    private void OnRemoveZip(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            Vm.ZipFiles.Remove(path);
    }

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

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Vm.S.AddZips;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;
        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var zips = items.OfType<StorageFile>()
                .Where(f => f.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Path);
            Vm.AddZips(zips);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnCopyErrors(object sender, RoutedEventArgs e)
    {
        if (Vm.ReportErrors.Count == 0)
            return;
        var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
        pkg.SetText(string.Join(Environment.NewLine, Vm.ReportErrors));
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
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
