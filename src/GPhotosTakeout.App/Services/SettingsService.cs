using System;
using System.IO;
using System.Text.Json;

namespace GPhotosTakeout.App.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>
/// Persists <see cref="AppSettings"/> as JSON under %LocalAppData%\GPhotosTakeout. A
/// plain file (rather than ApplicationData) works identically whether the app runs
/// packaged or unpackaged, so settings survive the planned move to MSIX.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPhotosTakeout");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable settings — fall back to defaults rather than crash.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; losing persistence is not fatal to the run.
        }
    }
}
