using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests;

public class CropCatalogTypeDisplayTests
{
    [Fact]
    public void Order_IsArableFruitVegetableIndustrialOrnamentalMedicinalAndAromatic_NotRawEnumValueOrder()
    {
        Assert.Equal(
            [CropCatalogType.Arable, CropCatalogType.Fruit, CropCatalogType.Vegetable, CropCatalogType.Industrial, CropCatalogType.Ornamental, CropCatalogType.MedicinalAndAromatic],
            CropCatalogTypeDisplay.Order);
    }

    [Fact]
    public void Order_ContainsEveryEnumValueExactlyOnce()
    {
        var allValues = Enum.GetValues<CropCatalogType>().ToHashSet();
        Assert.Equal(allValues.Count, CropCatalogTypeDisplay.Order.Count);
        Assert.Equal(allValues, CropCatalogTypeDisplay.Order.ToHashSet());
    }

    [Fact]
    public void DisplayName_MedicinalAndAromatic_HasAmpersand()
    {
        Assert.Equal("Medicinal & Aromatic", CropCatalogTypeDisplay.DisplayName(CropCatalogType.MedicinalAndAromatic));
    }

    [Fact]
    public void DisplayName_EveryOtherType_IsItsEnumName()
    {
        Assert.Equal("Arable", CropCatalogTypeDisplay.DisplayName(CropCatalogType.Arable));
        Assert.Equal("Fruit", CropCatalogTypeDisplay.DisplayName(CropCatalogType.Fruit));
        Assert.Equal("Vegetable", CropCatalogTypeDisplay.DisplayName(CropCatalogType.Vegetable));
        Assert.Equal("Industrial", CropCatalogTypeDisplay.DisplayName(CropCatalogType.Industrial));
        Assert.Equal("Ornamental", CropCatalogTypeDisplay.DisplayName(CropCatalogType.Ornamental));
    }
}
