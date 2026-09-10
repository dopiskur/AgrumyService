namespace Agrumy.Shared.Models
{
    /// Which of the four subcatalogs an entry belongs to - each backed by its own table (horticultureCatalogCrop/Perma/Hydroponic/Fruit), same row shape across all four.
    public enum HorticultureCatalogType
    {
        Crop = 1,
        Perma = 2,
        Hydroponic = 3,
        Fruit = 4,
    }

    /// Display order for catalog tabs/pickers - Hydroponic before Perma intentionally, opposite of the raw enum-value order above, so nothing here may use Enum.GetValues() for display.
    public static class HorticultureCatalogTypeDisplay
    {
        public static readonly IReadOnlyList<HorticultureCatalogType> Order =
        [
            HorticultureCatalogType.Crop,
            HorticultureCatalogType.Fruit,
            HorticultureCatalogType.Hydroponic,
            HorticultureCatalogType.Perma,
        ];
    }

    /// One recommended-parameter-range entry (e.g. "Tomato", "Food forest guild", "Deep water culture lettuce") - AirTemp/SoilTemp/AirHumidity/SoilMoisture/Light ranges drive HorticultureRuleTemplateBuilder's generated starter rules when applied to a zone; SoilPH/SoilEC/Co2 are informational only (no actuator exists in RelayFunction for pH/EC dosing or CO2 injection).
    public class HorticultureCatalogEntry
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
    }

    /// Result of applying a catalog entry's template to a zone - RulesSkipped names any rule the zone's existing rule-count cap stopped partway through (see DeviceFarmUnitApiController.AddRuleAsync's own cap check), not a validation failure.
    public class HorticultureCatalogApplyResult
    {
        public int RulesAdded { get; set; }
        public IList<string> RulesSkipped { get; set; } = [];
    }
}
