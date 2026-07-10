namespace GPhotosTakeout.App.Services;

/// <summary>User choices that persist between runs so the wizard remembers them.</summary>
public sealed record AppSettings
{
    public string? OutputDirectory { get; init; }
    public int OutputStructureIndex { get; init; }
    public int AlbumStrategyIndex { get; init; }
    public int DuplicateHandlingIndex { get; init; }
    /// <summary>Null = use the system timezone (resolved at load time).</summary>
    public string? FallbackTimeZone { get; init; }
    public bool DryRun { get; init; }

    /// <summary>UI language ("he" / "en"); null = follow the OS. Reserved for i18n.</summary>
    public string? Language { get; init; }
}
