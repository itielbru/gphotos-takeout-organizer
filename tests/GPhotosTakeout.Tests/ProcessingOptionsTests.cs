using GPhotosTakeout.Core.Models;
using Xunit;

namespace GPhotosTakeout.Tests;

public class ProcessingOptionsTests
{
    [Fact]
    public void GetDefaultFallbackTimeZone_ReturnsResolvableIanaId()
    {
        var id = ProcessingOptions.GetDefaultFallbackTimeZone();

        // Null is allowed only when the OS zone has no IANA mapping; when a value is
        // returned it must be resolvable and IANA-shaped (not a Windows display id).
        if (id is not null)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            Assert.NotNull(zone);
            Assert.True(id == "UTC" || id.Contains('/', StringComparison.Ordinal),
                $"'{id}' does not look like an IANA id");
        }
    }
}
