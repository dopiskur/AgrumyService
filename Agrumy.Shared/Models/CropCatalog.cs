namespace Agrumy.Shared.Models
{
    /// Which subcatalog an entry belongs to - each backed by its own table (cropCatalogArable/Fruit/Vegetable/Industrial/Ornamental/MedicinalAndAromatic), same row shape across all six (Arable alone also gets BBCH growth stages).
    public enum CropCatalogType
    {
        Arable = 1,
        Fruit = 2,
        Vegetable = 3,
        Industrial = 4,
        Ornamental = 5,
        MedicinalAndAromatic = 6,
    }

    /// Display order for catalog tabs/pickers.
    public static class CropCatalogTypeDisplay
    {
        public static readonly IReadOnlyList<CropCatalogType> Order =
        [
            CropCatalogType.Arable,
            CropCatalogType.Fruit,
            CropCatalogType.Vegetable,
            CropCatalogType.Industrial,
            CropCatalogType.Ornamental,
            CropCatalogType.MedicinalAndAromatic,
        ];

        public static string DisplayName(CropCatalogType type) => type switch
        {
            CropCatalogType.MedicinalAndAromatic => "Medicinal & Aromatic",
            _ => type.ToString(),
        };
    }

    /// One recommended-parameter-range entry (e.g. "Wheat - Winter", "Apple", "Lavender") - AirTemp/SoilTemp/AirHumidity/SoilMoisture/Light ranges drive CropCatalogRuleTemplateBuilder's generated starter rules when applied to a zone; SoilPH/SoilEC/Co2 are informational only (no actuator exists in RelayFunction for pH/EC dosing or CO2 injection).
    public class CropCatalogEntry
    {
        public int? ID { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }

        public double? AirTempMin { get; set; }
        public double? AirTempMax { get; set; }
        public double? SoilTempMin { get; set; }
        public double? SoilTempMax { get; set; }
        public double? AirHumidityMin { get; set; }
        public double? AirHumidityMax { get; set; }
        public double? SoilMoistureMin { get; set; }
        public double? SoilMoistureMax { get; set; }
        public double? LightMin { get; set; }
        public double? LightMax { get; set; }

        // Informational only - see this class's own remarks.
        public double? SoilPHMin { get; set; }
        public double? SoilPHMax { get; set; }
        public double? SoilECMin { get; set; }
        public double? SoilECMax { get; set; }
        public double? Co2Min { get; set; }
        public double? Co2Max { get; set; }

        // Type==Arable only (every other type never populates these) - variety/hybrid class identifier, free text since the convention differs per species.
        public string? ClassCode { get; set; }
        public string? PhaseDescriptionsJson { get; set; }
        public IList<CropCatalogGrowthStage> GrowthStages { get; set; } = [];
    }

    /// BBCH principal growth stages 0-9 (Zadoks-derived) - not every stage applies to every species, e.g. maize's own BBCH monograph never defines Tillering or Booting.
    public enum BbchGrowthStage
    {
        Germination = 0,
        LeafDevelopment = 1,
        Tillering = 2,
        StemElongation = 3,
        Booting = 4,
        Heading = 5,
        Flowering = 6,
        MilkDevelopment = 7,
        DoughDevelopment = 8,
        Ripening = 9,
    }

    /// One BBCH stage's environmental parameters for one CropCatalogEntry (Type==Arable) - DurationDaysMin/Max is this stage's OWN length, not cumulative from Sowing.StartDate; summing every stage's duration (see CropSeasons.cshtml's cropMaturityDays) estimates the crop's total days-to-maturity, and a running sum from StartDate lets the app estimate which stage a sowing currently sits in.
    public class CropCatalogGrowthStage
    {
        public int? ID { get; set; }
        public BbchGrowthStage StageNumber { get; set; }
        public double? AirTempMin { get; set; }
        public double? AirTempMax { get; set; }
        public double? SoilTempMin { get; set; }
        public double? SoilTempMax { get; set; }
        public double? AirHumidityMin { get; set; }
        public double? AirHumidityMax { get; set; }
        public double? SoilMoistureMin { get; set; }
        public double? SoilMoistureMax { get; set; }
        public double? LightMin { get; set; }
        public double? LightMax { get; set; }
        public int? DurationDaysMin { get; set; }
        public int? DurationDaysMax { get; set; }
    }

    /// Result of applying a catalog entry's template to a zone - RulesSkipped names any rule the zone's existing rule-count cap stopped partway through (see DeviceFarmUnitApiController.AddRuleAsync's own cap check), not a validation failure.
    public class CropCatalogApplyResult
    {
        public int RulesAdded { get; set; }
        public IList<string> RulesSkipped { get; set; } = [];
    }
}
