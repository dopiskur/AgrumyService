using Agrumy.Dal;
using Agrumy.Api.Dal;
using Agrumy.Api.Dal.Interface;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agrumy.Api.Tests.TestSupport
{
    /// Test-only composed facade over every EfXxxRepository, letting RelationalIntegrationTests drive many facets through one object - and, by implementing every facet interface itself, be passed anywhere a single narrow facet is expected - the way pre-extraction production code once did via IRepository (production consumers now inject narrow facets directly; see SchemaBootstrapper/DataSeeder for the schema/seed slice this used to own).
    internal sealed partial class AllFacetsRepository(AgrumyDbContext db, IAuditLogRepository auditLogRepository, IRefreshTokenRepository refreshTokenRepository, IControllerDataRepository controllerDataRepository, IDiscoveryRepository discoveryRepository, ITenantRepository tenantRepository, IGatewayRepository gatewayRepository, IServerConfigRepository serverConfigRepository, IDeviceOutboxRepository outboxRepository, IFirmwareRepository firmwareRepository, IUserRepository userRepository, IDeviceRepository deviceRepository, ISimulationRepository simulationRepository, IDeviceFarmUnitRepository deviceFarmUnitRepository, ISensorDataRepository sensorDataRepository, IExperimentRepository experimentRepository, IHorticultureCatalogRepository horticultureCatalogRepository, ISowingRepository sowingRepository, IFarmParcelRepository farmParcelRepository, ICropCatalogRepository cropCatalogRepository, IFieldLogRepository fieldLogRepository, IZonePlantingRepository zonePlantingRepository, ISatelliteConfigRepository satelliteConfigRepository, ISatelliteSceneRepository satelliteSceneRepository) :
        ISystemRepository, IServerConfigRepository, IUserRepository, ITenantRepository, IRefreshTokenRepository,
        IDeviceRepository, IDeviceFarmUnitRepository, IDeviceOutboxRepository, IFirmwareRepository, ISensorDataRepository,
        IAuditLogRepository, IGatewayRepository, IDiscoveryRepository, IControllerDataRepository, ISimulationRepository,
        IExperimentRepository, IHorticultureCatalogRepository, ISowingRepository, IFarmParcelRepository, ICropCatalogRepository, IFieldLogRepository, IZonePlantingRepository,
        ISatelliteConfigRepository, ISatelliteSceneRepository
    {
        public Task<bool> TestConnectionAsync() => db.Database.CanConnectAsync();

        public Task EnsureSchemaAsync() =>
            new SchemaBootstrapper(db, NullLogger<SchemaBootstrapper>.Instance, serverConfigRepository, new DataSeeder(NullLogger<DataSeeder>.Instance))
                .EnsureSchemaAsync();

        public DbFailureKind ClassifyException(Exception ex) => DbExceptionClassifier.Classify(ex);
    }
}
