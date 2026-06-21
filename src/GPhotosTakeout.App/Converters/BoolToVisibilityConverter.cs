using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GPhotosTakeout.App.Converters;

/// <summary>Maps true → Visible, false → Collapsed (WinUI has no built-in equivalent).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}
