namespace GPhotosTakeout.Core.Metadata;

/// <summary>The metadata to embed in one media file, already resolved to local time.</summary>
public sealed record ExifMetadata
{
    /// <summary>Local capture time to write to DateTimeOriginal / QuickTime CreateDate.</summary>
    public DateTime? DateTakenLocal { get; init; }

    /// <summary>UTC capture time (used for video QuickTime tags, which are UTC).</summary>
    public DateTime? DateTakenUtc { get; init; }

    /// <summary>UTC offset string like "+03:00" for OffsetTimeOriginal (images only).</summary>
    public string? Offset { get; init; }

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? Altitude { get; init; }

    public string? Description { get; init; }
    public bool Favorited { get; init; }

    public bool HasGps => Latitude is not null && Longitude is not null;
    public bool IsEmpty => DateTakenLocal is null && DateTakenUtc is null && !HasGps
                           && string.IsNullOrEmpty(Description) && !Favorited;
}
