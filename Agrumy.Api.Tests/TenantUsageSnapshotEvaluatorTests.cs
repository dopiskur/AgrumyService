using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises TenantUsageSnapshotEvaluator directly - no database, ITenantRepository is mocked.
public class TenantUsageSnapshotEvaluatorTests
{
    private readonly Mock<ITenantRepository> _tenants = new(MockBehavior.Strict);

    private TenantUsageSnapshotEvaluator NewEvaluator() => new(_tenants.Object);

    [Fact]
    public async Task RunOnceAsync_RecordsOneSnapshotPerRealTenant()
    {
        _tenants.Setup(t => t.TenantsGetAllAsync()).ReturnsAsync(new List<Tenant>
        {
            new() { IDTenant = 1, TenantName = "Farm A" },
            new() { IDTenant = 2, TenantName = "Farm B" },
        });
        _tenants.Setup(t => t.TenantUsageSnapshotRecordAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _tenants.Setup(t => t.TenantUsageSnapshotRecordAsync(2, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.TenantUsageSnapshotRecordAsync(1, It.IsAny<DateTimeOffset>()), Times.Once);
        _tenants.Verify(t => t.TenantUsageSnapshotRecordAsync(2, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_SkipsATenantRowWithNoId()
    {
        _tenants.Setup(t => t.TenantsGetAllAsync()).ReturnsAsync(new List<Tenant> { new() { IDTenant = null, TenantName = "Unclaimed" } });

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.TenantUsageSnapshotRecordAsync(It.IsAny<int>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }
}
