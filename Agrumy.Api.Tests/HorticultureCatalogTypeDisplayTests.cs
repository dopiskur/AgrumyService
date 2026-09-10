using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests;

public class HorticultureCatalogTypeDisplayTests
{
    [Fact]
    public void Order_IsCropFruitHydroponicPerma_NotRawEnumValueOrder()
    {
        Assert.Equal(
            [HorticultureCatalogType.Crop, HorticultureCatalogType.Fruit, HorticultureCatalogType.Hydroponic, HorticultureCatalogType.Perma],
            HorticultureCatalogTypeDisplay.Order);
    }

    [Fact]
    public void Order_ContainsEveryEnumValueExactlyOnce()
    {
        var allValues = Enum.GetValues<HorticultureCatalogType>().ToHashSet();
        Assert.Equal(allValues.Count, HorticultureCatalogTypeDisplay.Order.Count);
        Assert.Equal(allValues, HorticultureCatalogTypeDisplay.Order.ToHashSet());
    }
}
