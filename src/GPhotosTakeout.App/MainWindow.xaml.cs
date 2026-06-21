using System;
using System.Diagnostics;
using System.Linq;
using GPhotosTakeout.App.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GPhotosTakeout.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel Vm { get; }

    public MainWindow()
    {
        InitializeComponent();
        Vm = new MainViewModel(DispatcherQueue);
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
}
