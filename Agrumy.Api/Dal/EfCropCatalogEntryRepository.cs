using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// ICropCatalogEntryRepository - one near-identical code path per table, switched on CropCatalogType rather than six near-duplicate repository classes.
    internal sealed class EfCropCatalogEntryRepository(AgrumyDbContext db) : ICropCatalogEntryRepository
    {
        public async Task<IList<CropCatalogEntry>> CatalogGetAsync(CropCatalogType type)
        {
            switch (type)
            {
                case CropCatalogType.Arable:
                {
                    var rows = await db.CropCatalogArables.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
                    var stagesByArableId = (await db.CropCatalogArableGrowthStages.AsNoTracking()
                        .Where(s => rows.Select(r => r.ID).Contains(s.CropCatalogArableID)).ToListAsync())
                        .GroupBy(s => s.CropCatalogArableID).ToDictionary(g => g.Key, g => g.Select(ToDto).ToList());
                    return rows.Select(r => ToDto(r, (IList<CropCatalogGrowthStage>)stagesByArableId.GetValueOrDefault(r.ID, []))).ToList();
                }
                case CropCatalogType.Fruit:
                    return (await db.CropCatalogFruits.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                case CropCatalogType.Vegetable:
                    return (await db.CropCatalogVegetables.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                case CropCatalogType.Industrial:
                    return (await db.CropCatalogIndustrials.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                case CropCatalogType.Ornamental:
                    return (await db.CropCatalogOrnamentals.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                case CropCatalogType.MedicinalAndAromatic:
                    return (await db.CropCatalogMedicinalAndAromatics.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                default:
                    return [];
            }
        }

        public async Task<CropCatalogEntry?> CatalogGetByIdAsync(CropCatalogType type, int id)
        {
            switch (type)
            {
                case CropCatalogType.Arable:
                {
                    var row = await db.CropCatalogArables.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id);
                    if (row == null) return null;
                    var stages = (await db.CropCatalogArableGrowthStages.AsNoTracking().Where(s => s.CropCatalogArableID == id).ToListAsync()).Select(ToDto).ToList();
                    return ToDto(row, stages);
                }
                case CropCatalogType.Fruit:
                    return ToDtoOrNull(await db.CropCatalogFruits.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                case CropCatalogType.Vegetable:
                    return ToDtoOrNull(await db.CropCatalogVegetables.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                case CropCatalogType.Industrial:
                    return ToDtoOrNull(await db.CropCatalogIndustrials.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                case CropCatalogType.Ornamental:
                    return ToDtoOrNull(await db.CropCatalogOrnamentals.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                case CropCatalogType.MedicinalAndAromatic:
                    return ToDtoOrNull(await db.CropCatalogMedicinalAndAromatics.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                default:
                    return null;
            }
        }

        public async Task<CropCatalogEntry> CatalogAddAsync(CropCatalogType type, CropCatalogEntry entry)
        {
            switch (type)
            {
                case CropCatalogType.Arable:
                {
                    var row = new CropCatalogArableRow();
                    CopyToRow(entry, row);
                    db.CropCatalogArables.Add(row);
                    await db.SaveChangesAsync();
                    await ReplaceGrowthStagesAsync(row.ID, entry.GrowthStages);
                    return ToDto(row, entry.GrowthStages);
                }
                case CropCatalogType.Fruit:
                {
                    var row = new CropCatalogFruitRow();
                    CopyToRow(entry, row);
                    db.CropCatalogFruits.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                case CropCatalogType.Vegetable:
                {
                    var row = new CropCatalogVegetableRow();
                    CopyToRow(entry, row);
                    db.CropCatalogVegetables.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                case CropCatalogType.Industrial:
                {
                    var row = new CropCatalogIndustrialRow();
                    CopyToRow(entry, row);
                    db.CropCatalogIndustrials.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                case CropCatalogType.Ornamental:
                {
                    var row = new CropCatalogOrnamentalRow();
                    CopyToRow(entry, row);
                    db.CropCatalogOrnamentals.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                case CropCatalogType.MedicinalAndAromatic:
                {
                    var row = new CropCatalogMedicinalAndAromaticRow();
                    CopyToRow(entry, row);
                    db.CropCatalogMedicinalAndAromatics.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public async Task<bool> CatalogUpdateAsync(CropCatalogType type, CropCatalogEntry entry)
        {
            switch (type)
            {
                case CropCatalogType.Arable:
                {
                    var row = await db.CropCatalogArables.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    await ReplaceGrowthStagesAsync(row.ID, entry.GrowthStages);
                    return true;
                }
                case CropCatalogType.Fruit:
                {
                    var row = await db.CropCatalogFruits.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                case CropCatalogType.Vegetable:
                {
                    var row = await db.CropCatalogVegetables.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                case CropCatalogType.Industrial:
                {
                    var row = await db.CropCatalogIndustrials.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                case CropCatalogType.Ornamental:
                {
                    var row = await db.CropCatalogOrnamentals.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                case CropCatalogType.MedicinalAndAromatic:
                {
                    var row = await db.CropCatalogMedicinalAndAromatics.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                default:
                    return false;
            }
        }

        public async Task<bool> CatalogDeleteAsync(CropCatalogType type, int id)
        {
            int rows = type switch
            {
                CropCatalogType.Arable => await db.CropCatalogArables.Where(r => r.ID == id).ExecuteDeleteAsync(),
                CropCatalogType.Fruit => await db.CropCatalogFruits.Where(r => r.ID == id).ExecuteDeleteAsync(),
                CropCatalogType.Vegetable => await db.CropCatalogVegetables.Where(r => r.ID == id).ExecuteDeleteAsync(),
                CropCatalogType.Industrial => await db.CropCatalogIndustrials.Where(r => r.ID == id).ExecuteDeleteAsync(),
                CropCatalogType.Ornamental => await db.CropCatalogOrnamentals.Where(r => r.ID == id).ExecuteDeleteAsync(),
                CropCatalogType.MedicinalAndAromatic => await db.CropCatalogMedicinalAndAromatics.Where(r => r.ID == id).ExecuteDeleteAsync(),
                _ => 0,
            };
            return rows > 0;
        }

        private static void CopyToRow(CropCatalogEntry entry, CropCatalogArableRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
            row.ClassCode = entry.ClassCode;
            row.PhaseDescriptionsJson = entry.PhaseDescriptionsJson;
        }

        /// Full delete-then-reinsert rather than a diff - a handful of rows (at most 10, one per BBCH stage), never worth the extra complexity of matching old to new by StageNumber.
        private async Task ReplaceGrowthStagesAsync(int arableId, IList<CropCatalogGrowthStage> stages)
        {
            await db.CropCatalogArableGrowthStages.Where(s => s.CropCatalogArableID == arableId).ExecuteDeleteAsync();
            foreach (CropCatalogGrowthStage s in stages)
            {
                db.CropCatalogArableGrowthStages.Add(new CropCatalogArableGrowthStageRow
                {
                    CropCatalogArableID = arableId,
                    StageNumber = (int)s.StageNumber,
                    AirTempMin = s.AirTempMin, AirTempMax = s.AirTempMax,
                    SoilTempMin = s.SoilTempMin, SoilTempMax = s.SoilTempMax,
                    AirHumidityMin = s.AirHumidityMin, AirHumidityMax = s.AirHumidityMax,
                    SoilMoistureMin = s.SoilMoistureMin, SoilMoistureMax = s.SoilMoistureMax,
                    LightMin = s.LightMin, LightMax = s.LightMax,
                    DurationDaysMin = s.DurationDaysMin, DurationDaysMax = s.DurationDaysMax,
                });
            }
            if (stages.Count > 0)
            {
                await db.SaveChangesAsync();
            }
        }

        private static void CopyToRow(CropCatalogEntry entry, CropCatalogFruitRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static void CopyToRow(CropCatalogEntry entry, CropCatalogVegetableRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static void CopyToRow(CropCatalogEntry entry, CropCatalogIndustrialRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static void CopyToRow(CropCatalogEntry entry, CropCatalogOrnamentalRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static void CopyToRow(CropCatalogEntry entry, CropCatalogMedicinalAndAromaticRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static CropCatalogEntry? ToDtoOrNull(CropCatalogFruitRow? row) => row == null ? null : ToDto(row);
        private static CropCatalogEntry? ToDtoOrNull(CropCatalogVegetableRow? row) => row == null ? null : ToDto(row);
        private static CropCatalogEntry? ToDtoOrNull(CropCatalogIndustrialRow? row) => row == null ? null : ToDto(row);
        private static CropCatalogEntry? ToDtoOrNull(CropCatalogOrnamentalRow? row) => row == null ? null : ToDto(row);
        private static CropCatalogEntry? ToDtoOrNull(CropCatalogMedicinalAndAromaticRow? row) => row == null ? null : ToDto(row);

        private static CropCatalogGrowthStage ToDto(CropCatalogArableGrowthStageRow s) => new()
        {
            ID = s.ID,
            StageNumber = (BbchGrowthStage)s.StageNumber,
            AirTempMin = s.AirTempMin, AirTempMax = s.AirTempMax,
            SoilTempMin = s.SoilTempMin, SoilTempMax = s.SoilTempMax,
            AirHumidityMin = s.AirHumidityMin, AirHumidityMax = s.AirHumidityMax,
            SoilMoistureMin = s.SoilMoistureMin, SoilMoistureMax = s.SoilMoistureMax,
            LightMin = s.LightMin, LightMax = s.LightMax,
            DurationDaysMin = s.DurationDaysMin, DurationDaysMax = s.DurationDaysMax,
        };

        private static CropCatalogEntry ToDto(CropCatalogArableRow row, IList<CropCatalogGrowthStage> stages) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
            ClassCode = row.ClassCode,
            PhaseDescriptionsJson = row.PhaseDescriptionsJson,
            GrowthStages = stages.OrderBy(s => s.StageNumber).ToList(),
        };

        private static CropCatalogEntry ToDto(CropCatalogFruitRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };

        private static CropCatalogEntry ToDto(CropCatalogVegetableRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };

        private static CropCatalogEntry ToDto(CropCatalogIndustrialRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };

        private static CropCatalogEntry ToDto(CropCatalogOrnamentalRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };

        private static CropCatalogEntry ToDto(CropCatalogMedicinalAndAromaticRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };
    }
}
