using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Migration;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises TenantImportService.ImportUsersAsync's Global-role stripping - a Global-scope role is a server-level concept and must never survive an organization export/import round-trip onto a different server.
public class TenantImportServiceTests
{
    // ITenantRepository is the primary mocked type; IUserRepository/IDeviceFarmUnitRepository are the same underlying mock viewed through Mock.As<T>() - IDeviceRepository/ISensorDataRepository are never called by ImportByNameAsync's user-only path here, so they're separate, unconfigured strict mocks.
    private readonly Mock<ITenantRepository> _repo = new(MockBehavior.Strict);
    private IUserRepository UserRepo => _repo.As<IUserRepository>().Object;
    private IDeviceFarmUnitRepository FarmUnitRepo => _repo.As<IDeviceFarmUnitRepository>().Object;

    private TenantImportService NewService() => new(_repo.Object, UserRepo, FarmUnitRepo,
        new Mock<IDeviceRepository>(MockBehavior.Strict).Object, new Mock<ISensorDataRepository>(MockBehavior.Strict).Object,
        new Mock<IFarmParcelRepository>(MockBehavior.Strict).Object,
        new Mock<ISowingRepository>(MockBehavior.Strict).Object, new Mock<ICropCatalogRepository>(MockBehavior.Strict).Object,
        new Mock<IFieldLogRepository>(MockBehavior.Strict).Object, new Mock<IZonePlantingRepository>(MockBehavior.Strict).Object);

    private static TenantExport Export(params TenantExportUser[] users) => new()
    {
        Users = users,
    };

    [Fact]
    public async Task ImportedUser_GlobalAdminRole_IsStrippedBeforeApplying()
    {
        var exportedUser = new TenantExportUser
        {
            User = new User { IDUser = 1, Email = "boss@source.local" },
            PwdHash = "hash",
            PwdSalt = "salt",
            Roles = new List<string> { RoleNames.GlobalAdmin, RoleNames.TenantUser },
        };

        _repo.Setup(r => r.TenantGetIdAsync("Acme")).ReturnsAsync(5);
        _repo.As<IDeviceFarmUnitRepository>().Setup(r => r.EnsureFirstFarmAsync(5)).Returns(Task.CompletedTask);
        _repo.As<IUserRepository>().SetupSequence(r => r.UserGetAsync(null, "boss@source.local", null))
             .ReturnsAsync((User?)null)
             .ReturnsAsync(new User { IDUser = 42, Email = "boss@source.local" });
        _repo.As<IUserRepository>().Setup(r => r.UserAddAsync(It.IsAny<User>(), It.IsAny<UserSecret>())).Returns(Task.CompletedTask);
        List<string>? appliedRoles = null;
        _repo.As<IUserRepository>().Setup(r => r.UserRolesSetAsync(42, It.IsAny<IEnumerable<string>>()))
             .Callback<int, IEnumerable<string>>((_, roles) => appliedRoles = roles.ToList())
             .Returns(Task.CompletedTask);

        await NewService().ImportByNameAsync(Export(exportedUser), "Acme");

        Assert.NotNull(appliedRoles);
        Assert.DoesNotContain(RoleNames.GlobalAdmin, appliedRoles);
        Assert.Contains(RoleNames.TenantUser, appliedRoles);
    }

    [Fact]
    public async Task ImportedUser_NoGlobalRole_AllRolesKept()
    {
        var exportedUser = new TenantExportUser
        {
            User = new User { IDUser = 1, Email = "member@source.local" },
            Roles = new List<string> { RoleNames.TenantAdmin, RoleNames.TenantDevice },
        };

        _repo.Setup(r => r.TenantGetIdAsync("Acme")).ReturnsAsync(5);
        _repo.As<IDeviceFarmUnitRepository>().Setup(r => r.EnsureFirstFarmAsync(5)).Returns(Task.CompletedTask);
        _repo.As<IUserRepository>().SetupSequence(r => r.UserGetAsync(null, "member@source.local", null))
             .ReturnsAsync((User?)null)
             .ReturnsAsync(new User { IDUser = 43, Email = "member@source.local" });
        _repo.As<IUserRepository>().Setup(r => r.UserAddAsync(It.IsAny<User>(), It.IsAny<UserSecret>())).Returns(Task.CompletedTask);
        List<string>? appliedRoles = null;
        _repo.As<IUserRepository>().Setup(r => r.UserRolesSetAsync(43, It.IsAny<IEnumerable<string>>()))
             .Callback<int, IEnumerable<string>>((_, roles) => appliedRoles = roles.ToList())
             .Returns(Task.CompletedTask);

        await NewService().ImportByNameAsync(Export(exportedUser), "Acme");

        Assert.Equal(new[] { RoleNames.TenantAdmin, RoleNames.TenantDevice }, appliedRoles);
    }
}
