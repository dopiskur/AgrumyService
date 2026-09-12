using System.Security.Cryptography;
using System.Text;
using Agrumy.Api.Satellite;
using Agrumy.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace Agrumy.Api.Tests;

public class EvalscriptsTests(ITestOutputHelper output)
{
    [Fact]
    public void All_HasExactlySixIndices_MatchingTheRoadmapsCatalog()
    {
        Assert.Equal(6, Evalscripts.All.Count);
        foreach (SatelliteIndex index in Enum.GetValues<SatelliteIndex>())
        {
            Assert.True(Evalscripts.All.ContainsKey(index), $"Missing evalscript for {index}.");
        }
    }

    [Theory]
    [InlineData(SatelliteIndex.Ndvi, true)]
    [InlineData(SatelliteIndex.Ndmi, true)]
    [InlineData(SatelliteIndex.Ndwi, true)]
    [InlineData(SatelliteIndex.Ndsi, true)]
    [InlineData(SatelliteIndex.SwirComposite, false)]
    [InlineData(SatelliteIndex.NaturalColor, false)]
    public void HasStatistics_MatchesWhetherTheIndexIsAScalarOrAVisualComposite(SatelliteIndex index, bool expected) =>
        Assert.Equal(expected, Evalscripts.All[index].HasStatistics);

    [Fact]
    public void EveryEvalscript_DeclaresBothDefaultAndDataMaskOutputs()
    {
        foreach ((SatelliteIndex index, Evalscripts.Definition def) in Evalscripts.All)
        {
            Assert.Contains("id: \"default\"", def.Script);
            Assert.Contains("id: \"dataMask\"", def.Script);
            Assert.Contains("isValidScl", def.Script);
        }
    }

    /// A change to any evalscript string is a deliberate, reviewable diff - if this fails, update the expected
    /// hash in the same commit as the evalscript change, never as an incidental side effect of something else.
    [Fact]
    public void Checksums_MatchTheLastReviewedVersion()
    {
        var expected = new Dictionary<SatelliteIndex, string>
        {
            [SatelliteIndex.Ndvi] = "89D027711C30F055AB63C79867129320662541BC400E33B5A6A3005A6A391F2D",
            [SatelliteIndex.Ndmi] = "E1E765D70CE556715F227F9A67DF3EDE669E95E10330A40D2AB8037F11C4C313",
            [SatelliteIndex.Ndwi] = "C11E5F81B161A34F1E27810645C4823135CA74198F48BB55C758200DD46BFCD5",
            [SatelliteIndex.Ndsi] = "97B3A0DF7B26F4232A4495A3275BE850812CE0DC8E6472206F8E2CB947B7BA46",
            [SatelliteIndex.SwirComposite] = "AF6D2D5501DD00F2B65EA06837E6C92A5B2CEC3CE9A7EF7D7005715961CDAD87",
            [SatelliteIndex.NaturalColor] = "E1FE88131F48EE76F32A05AEA4FD46C11E84382E24A7B8507F37F47A9D8D7DED",
        };

        foreach ((SatelliteIndex index, Evalscripts.Definition def) in Evalscripts.All)
        {
            string actual = Sha256(def.Script);
            output.WriteLine($"{index}: {actual}");
            Assert.Equal(expected[index], actual);
        }
    }

    [Fact]
    public void ScalarIndices_RenderAsUint8ButAggregateAsFloat32()
    {
        foreach ((SatelliteIndex index, Evalscripts.Definition def) in Evalscripts.All.Concat(Evalscripts.PlanetScope))
        {
            Assert.DoesNotContain("FLOAT32", def.RenderScript);
            if (def.HasStatistics)
            {
                Assert.Contains("sampleType: \"FLOAT32\"", def.Script);
                Assert.Contains($"Math.min({Evalscripts.QuantizedMax},", def.RenderScript);
            }
            else
            {
                Assert.Same(def.Script, def.RenderScript);
            }
        }
        Assert.Equal(-1, Evalscripts.Dequantize(0));
        Assert.Equal(1, Evalscripts.Dequantize(Evalscripts.QuantizedMax));
    }

    [Fact]
    public void RenderChecksums_MatchTheLastReviewedVersion()
    {
        var expected = new Dictionary<SatelliteIndex, string>
        {
            [SatelliteIndex.Ndvi] = "DB7006447AAFA29C5C6CDFD9AC01695887F1BBC7D8C1AD811A880F78913B55B4",
            [SatelliteIndex.Ndmi] = "9A07C4753598127BC98BF3F9C5DA33D3B2A8923EBB4B04F09B14BA83FE5344BB",
            [SatelliteIndex.Ndwi] = "8170382AE1AF032C8763AD52621AC6B4B62C75C8736BE24F841FA137B6D0783F",
            [SatelliteIndex.Ndsi] = "F6BB2B2F3F1DAED30E955B9C25AEF0C3710475B6108FBE206372D9E2978304C6",
            [SatelliteIndex.SwirComposite] = "AF6D2D5501DD00F2B65EA06837E6C92A5B2CEC3CE9A7EF7D7005715961CDAD87",
            [SatelliteIndex.NaturalColor] = "E1FE88131F48EE76F32A05AEA4FD46C11E84382E24A7B8507F37F47A9D8D7DED",
        };

        Dictionary<SatelliteIndex, string> actual = Evalscripts.All.ToDictionary(kv => kv.Key, kv => Sha256(kv.Value.RenderScript));
        foreach ((SatelliteIndex index, string hash) in actual)
        {
            output.WriteLine($"render {index}: {hash}");
        }
        Assert.Equal(expected, actual);
    }

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
