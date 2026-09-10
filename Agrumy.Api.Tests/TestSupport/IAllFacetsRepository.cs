using Agrumy.Api.Dal.Interface;

namespace Agrumy.Api.Tests.TestSupport
{
    /// Test-only composed mock target - lets one strict Mock&lt;IAllFacetsRepository&gt; satisfy every narrow facet a controller/service constructor takes, the way a single IRepository mock did before production code moved off that broad interface. Public (not internal) because Castle DynamicProxy/Moq can't proxy a non-public interface without an InternalsVisibleTo grant.
    public interface IAllFacetsRepository :
        ISystemRepository,
        IServerConfigRepository,
        IUserRepository,
        ITenantRepository,
        IRefreshTokenRepository,
        IDeviceRepository,
        IDeviceFarmUnitRepository,
        IFarmOpenfieldRepository,
        IDeviceOutboxRepository,
        IFirmwareRepository,
        ISensorDataRepository,
        IAuditLogRepository,
        IGatewayRepository,
        IDiscoveryRepository,
        IControllerDataRepository,
        ISimulationRepository,
        IExperimentRepository,
        IHorticultureCatalogRepository
    {
    }
}
