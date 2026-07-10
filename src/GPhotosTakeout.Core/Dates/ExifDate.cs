namespace GPhotosTakeout.Core.Dates;

/// <summary>
/// A capture time read from embedded metadata. EXIF date/time tags are naive local
/// wall-clock values (<see cref="IsUtc"/> = false); QuickTime/MP4 creation times are
/// UTC instants (<see cref="IsUtc"/> = true).
/// </summary>
public readonly record struct ExifDate(DateTime Value, bool IsUtc);
