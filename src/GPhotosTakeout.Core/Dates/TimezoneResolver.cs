using GeoTimeZone;
using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Core.Dates;

/// <summary>
/// Resolves the local time and UTC offset for a photo so we can write a correct
/// EXIF:DateTimeOriginal + EXIF:OffsetTimeOriginal (instead of a UTC time shown
/// at the wrong hour). The offset is derived from the photo's GPS location when
/// available; otherwise from a user-configured fallback timezone; otherwise the
/// time is treated as UTC with no offset.
/// </summary>
public sealed class TimezoneResolver
{
    private readonly TimeZoneInfo? _fallback;

    public TimezoneResolver(string? fallbackIanaId = null)
    {
        _fallback = TryFindTimeZone(fallbackIanaId);
    }

    /// <summary>
    /// Returns the local DateTimeOffset for the given UTC instant, using the GPS
    /// location if present, else the fallback timezone, else UTC (offset zero).
    /// </summary>
    public DateTimeOffset ResolveLocal(DateTimeOffset utc, TakeoutGeo? geo)
    {
        var tz = ResolveZone(geo);
        if (tz is null)
            return utc.ToUniversalTime(); // offset +00:00

        return TimeZoneInfo.ConvertTime(utc.ToUniversalTime(), tz);
    }

    /// <summary>The timezone for this location, falling back as configured. May be null.</summary>
    public TimeZoneInfo? ResolveZone(TakeoutGeo? geo)
    {
        if (geo is { IsPresent: true })
        {
            var ianaId = TimeZoneLookup.GetTimeZone(geo.Latitude, geo.Longitude).Result;
            var tz = TryFindTimeZone(ianaId);
            if (tz is not null)
                return tz;
        }
        return _fallback;
    }

    private static TimeZoneInfo? TryFindTimeZone(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
            return null;
        try
        {
            // .NET on Windows accepts IANA ids via ICU since .NET 6.
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
