namespace Agrumy.Dal.Entities
{
    // Three tables, identical shape - kept as separate classes/tables (not one polymorphic table) per the catalog's own design: Crop culture, Permaculture, and Hydroponics are conceptually distinct enough to browse/manage separately even though today's parameter set happens to match across all three.

    public class HorticultureCatalogCropRow
    {
        public int ID { get; set; }
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
        public double? SoilPHMin { get; set; }
        public double? SoilPHMax { get; set; }
        public double? SoilECMin { get; set; }
        public double? SoilECMax { get; set; }
        public double? Co2Min { get; set; }
        public double? Co2Max { get; set; }
        // Variety/hybrid class identifier - free text since the convention differs per species (Croatian wheat quality group e.g. "B1/A2", corn FAO maturity number e.g. "FAO 500").
        public string? ClassCode { get; set; }
        // {"0": "...", "1": "...", ..., "9": "..."} - one human-readable expectation per BBCH principal stage, shown to the user for whichever stage a sowing currently sits in.
        public string? PhaseDescriptionsJson { get; set; }
    }

    /// One BBCH principal growth stage (0-9) worth of environmental parameters for a HorticultureCatalogCropRow - not every stage applies to every species (maize's own BBCH monograph never defines 2/Tillering or 4/Booting), so StageNumber is a sparse key, not a fixed 0-9 sequence.
    public class HorticultureCatalogCropGrowthStageRow
    {
        public int ID { get; set; }
        public int HorticultureCatalogCropID { get; set; }
        public int StageNumber { get; set; }
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
        // Typical elapsed-day range for this stage, counted from Sowing.StartDate - lets the app estimate which stage a sowing currently sits in.
        public int? DurationDaysMin { get; set; }
        public int? DurationDaysMax { get; set; }
    }

    public class HorticultureCatalogPermaRow
    {
        public int ID { get; set; }
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
        public double? SoilPHMin { get; set; }
        public double? SoilPHMax { get; set; }
        public double? SoilECMin { get; set; }
        public double? SoilECMax { get; set; }
        public double? Co2Min { get; set; }
        public double? Co2Max { get; set; }
    }

    public class HorticultureCatalogHydroponicRow
    {
        public int ID { get; set; }
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
        public double? SoilPHMin { get; set; }
        public double? SoilPHMax { get; set; }
        public double? SoilECMin { get; set; }
        public double? SoilECMax { get; set; }
        public double? Co2Min { get; set; }
        public double? Co2Max { get; set; }
    }

    public class HorticultureCatalogFruitRow
    {
        public int ID { get; set; }
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
        public double? SoilPHMin { get; set; }
        public double? SoilPHMax { get; set; }
        public double? SoilECMin { get; set; }
        public double? SoilECMax { get; set; }
        public double? Co2Min { get; set; }
        public double? Co2Max { get; set; }
    }
}
