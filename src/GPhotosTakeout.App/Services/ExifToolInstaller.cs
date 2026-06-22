using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace GPhotosTakeout.App.Services;

/// <summary>
/// Downloads and installs a pinned ExifTool build into the app's <c>Tools/</c> folder so a
/// first-run user can fix the "ExifTool not found" wall with one click. No telemetry: this
/// only fetches the official ExifTool zip on explicit user action.
/// </summary>
public sealed class ExifToolInstaller
{
    public const string Version = "13.59";

    // Optional SHA-256 of exiftool-<Version>_64.zip. Leave empty to skip verification;
    // set it to enforce integrity (get it with: certutil -hashfile <zip> SHA256).
    public const string Sha256 = "";

    private static readonly string[] Mirrors =
    {
        $"https://exiftool.org/exiftool-{Version}_64.zip",
        $"https://master.dl.sourceforge.net/project/exiftool/exiftool-{Version}_64.zip?viasf=1",
    };

    /// <summary>The folder ExifToolLocator searches (next to the executable).</summary>
    public static string TargetDir => Path.Combine(AppContext.BaseDirectory, "Tools");

    /// <summary>Downloads + installs ExifTool. Returns the resulting exiftool.exe path.</summary>
    public static async Task<string> InstallAsync(IProgress<double>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(TargetDir);
        var tmpZip = Path.Combine(Path.GetTempPath(), $"exiftool-{Version}-{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadAsync(tmpZip, progress, ct).ConfigureAwait(false);
            VerifyChecksum(tmpZip);
            ExtractInto(tmpZip, TargetDir);
            var exe = Path.Combine(TargetDir, "exiftool.exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException("The downloaded archive did not contain exiftool.exe.");
            return exe;
        }
        finally
        {
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { /* best effort cleanup */ }
        }
    }

    private static async Task DownloadAsync(string dest, IProgress<double>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        Exception? last = null;
        foreach (var url in Mirrors)
        {
            try
            {
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1L;
                await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var dst = File.Create(dest);
                var buffer = new byte[81920];
                long readTotal = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    readTotal += n;
                    if (total > 0) progress?.Report((double)readTotal / total);
                }
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
            }
        }
        throw new InvalidOperationException("Could not download ExifTool from any mirror.", last);
    }

    private static void VerifyChecksum(string file)
    {
        if (string.IsNullOrWhiteSpace(Sha256))
            return;
        using var fs = File.OpenRead(file);
        var hash = Convert.ToHexString(SHA256.HashData(fs));
        if (!hash.Equals(Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"ExifTool checksum mismatch (expected {Sha256}, got {hash}).");
    }

    private static void ExtractInto(string zip, string targetDir)
    {
        var extract = Path.Combine(Path.GetTempPath(), $"exiftool-x-{Guid.NewGuid():N}");
        try
        {
            ZipFile.ExtractToDirectory(zip, extract);
            var root = Directory.GetDirectories(extract).FirstOrDefault() ?? extract;
            var exe = Directory.GetFiles(root, "exiftool*.exe").FirstOrDefault()
                      ?? throw new FileNotFoundException("exiftool executable not found in archive.");
            File.Copy(exe, Path.Combine(targetDir, "exiftool.exe"), overwrite: true);

            var filesDir = Path.Combine(root, "exiftool_files");
            if (Directory.Exists(filesDir))
                CopyDir(filesDir, Path.Combine(targetDir, "exiftool_files"));

            var readme = Directory.GetFiles(root, "README*").FirstOrDefault();
            if (readme is not null)
                File.Copy(readme, Path.Combine(targetDir, "exiftool-LICENSE.txt"), overwrite: true);
        }
        finally
        {
            try { if (Directory.Exists(extract)) Directory.Delete(extract, recursive: true); } catch { /* best effort */ }
        }
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(from, to, StringComparison.Ordinal));
        foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(f, f.Replace(from, to, StringComparison.Ordinal), overwrite: true);
    }
}
