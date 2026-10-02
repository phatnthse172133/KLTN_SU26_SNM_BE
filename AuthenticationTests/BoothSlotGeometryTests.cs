using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;
using Xunit;

namespace AuthenticationTests;

public sealed class BoothSlotGeometryTests
{
    [Fact]
    public void TryResolveFootprint_UsesLegacyZoneDefaults_WhenBlockMetadataIsMissing()
    {
        var block = new LayoutBlock
        {
            Name = "Legacy zone",
            ConfigJson = null,
            Zone = new Zone
            {
                DefaultBoothWidth = 80,
                DefaultBoothHeight = 60
            }
        };

        var resolved = BoothSlotGeometry.TryResolveFootprint(block, out var footprint, out var error);

        Assert.True(resolved);
        Assert.Null(error);
        Assert.Equal(80, footprint.Width);
        Assert.Equal(60, footprint.Height);
        Assert.Equal(0, footprint.InnerPadding);
    }

    [Fact]
    public void TryResolveFootprint_RejectsMissingMetadata_WhenNoLegacyZoneIsAvailable()
    {
        var block = new LayoutBlock
        {
            Name = "Broken zone",
            ConfigJson = null
        };

        var resolved = BoothSlotGeometry.TryResolveFootprint(block, out _, out var error);

        Assert.False(resolved);
        Assert.Contains("no configured booth footprint", error);
    }
}
