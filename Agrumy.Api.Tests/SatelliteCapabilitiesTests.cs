using Agrumy.Api.Satellite;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Agrumy.Api.Tests;

/// S-B2 - "capabilities po kolekciji; UI ne nudi NDMI za PlanetScope" (the roadmap's own test list for this session). GetCapabilities is a pure lookup, no HTTP involved, so a real CdseSentinelHubSource instance with throwaway dependencies is enough.
public class SatelliteCapabilitiesTests
{
    private static CdseSentinelHubSource NewSource() =>
        new(new HttpClient(), Mock.Of<ICdseTokenProvider>(), NullLogger<CdseSentinelHubSource>.Instance);

    [Fact]
    public void Sentinel2_HasAllSixIndices_IncludingSwirDerivedOnes()
    {
        SatelliteCapabilities caps = NewSource().GetCapabilities(SatelliteCollection.Sentinel2);

        Assert.Equal(6, caps.Indices.Count);
        Assert.Contains(SatelliteIndex.Ndmi, caps.Indices);
        Assert.Contains(SatelliteIndex.Ndsi, caps.Indices);
        Assert.True(caps.SupportsStatistics);
        Assert.Equal(10, caps.ResolutionMeters);
    }

    [Fact]
    public void PlanetScope_HasNoSwirDerivedIndices_NoNdmiNoNdsiNoSwirComposite()
    {
        SatelliteCapabilities caps = NewSource().GetCapabilities(SatelliteCollection.PlanetScope);

        Assert.DoesNotContain(SatelliteIndex.Ndmi, caps.Indices);
        Assert.DoesNotContain(SatelliteIndex.Ndsi, caps.Indices);
        Assert.DoesNotContain(SatelliteIndex.SwirComposite, caps.Indices);
        Assert.Contains(SatelliteIndex.Ndvi, caps.Indices);
        Assert.Equal(3, caps.ResolutionMeters);
        Assert.Equal(1, caps.RevisitDays);
    }

    [Fact]
    public void PleiadesSpot_OnlyNaturalColorAndNdvi_NoStatisticsSupport()
    {
        SatelliteCapabilities caps = NewSource().GetCapabilities(SatelliteCollection.PleiadesSpot);

        Assert.Equal(2, caps.Indices.Count);
        Assert.Contains(SatelliteIndex.Ndvi, caps.Indices);
        Assert.Contains(SatelliteIndex.NaturalColor, caps.Indices);
        Assert.False(caps.SupportsStatistics);
    }

    [Fact]
    public void PlanetScopeEvalscripts_MatchTheirOwnCapabilitiesIndexList()
    {
        SatelliteCapabilities caps = NewSource().GetCapabilities(SatelliteCollection.PlanetScope);

        foreach (SatelliteIndex index in caps.Indices)
        {
            Assert.True(Evalscripts.PlanetScope.ContainsKey(index), $"PlanetScope capabilities advertise {index} but no evalscript exists for it.");
        }
    }

    [Fact]
    public void PlanetScopeEvalscripts_UseRealPlanetBandNames_NotSentinel2Bxx()
    {
        foreach ((SatelliteIndex _, Evalscripts.Definition def) in Evalscripts.PlanetScope)
        {
            Assert.DoesNotContain("B04", def.Script);
            Assert.DoesNotContain("B08", def.Script);
            Assert.Contains("clear", def.Script);
        }
    }
}
