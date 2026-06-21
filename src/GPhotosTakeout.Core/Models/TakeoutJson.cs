using System.Text.Json;
using System.Text.Json.Serialization;

namespace GPhotosTakeout.Core.Models;

/// <summary>
/// The subset of a Google Photos JSON sidecar we care about. Field names follow
/// Google's schema. Times are Unix-epoch seconds in UTC; geo is decimal degrees
/// (often 0/0 when no location is known).
/// </summary>
public sealed class TakeoutJson
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("photoTakenTime")] public TakeoutTimestamp? PhotoTakenTime { get; set; }
    [JsonPropertyName("creationTime")] public TakeoutTimestamp? CreationTime { get; set; }
    [JsonPropertyName("geoData")] public TakeoutGeo? GeoData { get; set; }
    [JsonPropertyName("geoDataExif")] public TakeoutGeo? GeoDataExif { get; set; }
    [JsonPropertyName("favorited")] public bool Favorited { get; set; }

    /// <summary>The effective capture instant in UTC, preferring photoTakenTime.</summary>
    public DateTimeOffset? CapturedUtc =>
        (PhotoTakenTime ?? CreationTime)?.ToUtc();

    /// <summary>The best available location, preferring geoData over geoDataExif, ignoring 0/0.</summary>
    public TakeoutGeo? BestGeo =>
        (GeoData is { IsPresent: true } ? GeoData : null)
        ?? (GeoDataExif is { IsPresent: true } ? GeoDataExif : null);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static TakeoutJson? Parse(string json) =>
        JsonSerializer.Deserialize<TakeoutJson>(json, Options);
}

public sealed class TakeoutTimestamp
{
    /// <summary>Unix epoch seconds (UTC), serialized by Google as a string.</summary>
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
    [JsonPropertyName("formatted")] public string? Formatted { get; set; }

    public DateTimeOffset? ToUtc() =>
        long.TryParse(Timestamp, out var secs)
            ? DateTimeOffset.FromUnixTimeSeconds(secs)
            : null;
}

public sealed class TakeoutGeo
{
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("altitude")] public double Altitude { get; set; }

    /// <summary>Google writes 0/0 when there is no location; treat that as absent.</summary>
    public bool IsPresent => Latitude != 0.0 || Longitude != 0.0;
}
