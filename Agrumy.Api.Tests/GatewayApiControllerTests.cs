using System.Buffers.Binary;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agrumy.Api.Commands;
using Agrumy.Api.Controllers.API;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Agrumy.Api.Tests.TestSupport;

namespace Agrumy.Api.Tests;

/// A gateway must only forward entries for devices in its own organization, never cross an organization boundary.
public class GatewayApiControllerTests
{
    private readonly Mock<IAllFacetsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();

    // Gateway organization-boundary behavior, not quota behavior - every organization is unlimited by default here.
    public GatewayApiControllerTests() => _repo.Setup(r => r.TenantQuotaGetAsync(It.IsAny<int>())).ReturnsAsync((TenantQuota?)null);

    private GatewayApiController NewController(string callerApiId)
    {
        var controller = NewJwtController();
        var http = new DefaultHttpContext();
        http.Items[DeviceAuth.ApiIdItemKey] = callerApiId;
        controller.ControllerContext = new() { HttpContext = http };
        return controller;
    }

    private GatewayApiController NewJwtController()
    {
        var catalog = FirmwareTestSupport.NewCatalog(_repo.Object, _repo.Object, _repo.Object);
        var outboxService = new DeviceOutboxService(_repo.Object, _repo.Object, _repo.Object, _repo.Object, new NoOpMqttCommandPublisher());
        return new GatewayApiController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object,
            outboxService, catalog,
            new Agrumy.Api.Devices.DeviceConfigBuilder(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, catalog, outboxService),
            new Agrumy.Api.Quota.TenantQuotaEnforcer(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object), NullLogger<GatewayApiController>.Instance);
    }

    /// Gives a bare (non-DI-constructed) controller the JWT claims an [Authorize] action reads via HttpContext.User - same pattern as ApiControllerTests.SetCallerRoles.
    private static void SetCallerRoles(ControllerBase controller, int? tenantId, params string[] roles)
    {
        var claims = new List<Claim> { new("TenantID", tenantId.ToString() ?? "") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
    }

    private static JsonElement EmptyPayload() => JsonDocument.Parse("{}").RootElement;

    /// Mirrors AgrumyFirmware's LoRaPrivateController wire format via the production Encrypt itself - base64, since that's what GatewayRelayUplinkRequest.Payload carries.
    private static string EncryptForWire(byte[] masterKey, byte[] bootNonce, uint counter, string plaintext) =>
        Convert.ToBase64String(Agrumy.Shared.LoRa.LoRaPrivatePayloadCrypto.Encrypt(masterKey, bootNonce, counter, plaintext));

    private static readonly byte[] TestLoRaKey = Convert.FromHexString("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
    private static readonly byte[] TestBootNonce = Convert.FromHexString("0102030405060708");

    [Fact]
    public async Task Batch_DeviceInDifferentTenant_RejectsEntryWithoutForwarding()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "gateway1", IsGateway = true, TenantID = 1 };
        var device = new Device { IDDevice = 2, ApiId = "dev1", ApiKey = "key1", TenantID = 2 };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("gateway1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.DeviceGetByApiIdAsync("dev1")).ReturnsAsync(device);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });

        var controller = NewController("gateway1");
        var response = await controller.Batch(new GatewayBatchRequest
        {
            Entries = [new GatewayBatchEntry { DeviceApiId = "dev1", DeviceApiKey = "key1", Type = GatewayEntryType.Event, Payload = EmptyPayload() }]
        });

        var result = Assert.Single(Assert.IsType<GatewayBatchResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result).Value).Results);
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        // Strict mock: an un-set-up EventDevicePushAsync call would throw, proving the cross-organization entry was never forwarded.
    }

    [Fact]
    public async Task Batch_DeviceInSameTenant_Forwards()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "gateway1", IsGateway = true, TenantID = 1 };
        var device = new Device { IDDevice = 2, ApiId = "dev1", ApiKey = "key1", TenantID = 1 };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("gateway1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.DeviceGetByApiIdAsync("dev1")).ReturnsAsync(device);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GetOutboxItemByIdAsync(5)).ReturnsAsync((DeviceCommand?)null);

        var payload = JsonDocument.Parse("{\"CommandId\":5}").RootElement;
        var controller = NewController("gateway1");
        var response = await controller.Batch(new GatewayBatchRequest
        {
            Entries = [new GatewayBatchEntry { DeviceApiId = "dev1", DeviceApiKey = "key1", Type = GatewayEntryType.CommandAck, Payload = payload }]
        });

        var result = Assert.Single(Assert.IsType<GatewayBatchResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result).Value).Results);
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    /// A GatewayDeviceToken (what GET /api/Gateway/DeviceMapping now hands out instead of a device's real ApiKey) must be accepted by Batch alongside the device's genuine ApiKey.
    [Fact]
    public async Task Batch_ValidGatewayDeviceToken_Forwards()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "gateway1", IsGateway = true, TenantID = 1 };
        var device = new Device { IDDevice = 2, ApiId = "dev1", ApiKey = "realKey", TenantID = 1 };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("gateway1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.DeviceGetByApiIdAsync("dev1")).ReturnsAsync(device);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GetOutboxItemByIdAsync(5)).ReturnsAsync((DeviceCommand?)null);

        string token = GatewayDeviceToken.Issue("dev1", "realKey");
        var payload = JsonDocument.Parse("{\"CommandId\":5}").RootElement;
        var controller = NewController("gateway1");
        var response = await controller.Batch(new GatewayBatchRequest
        {
            Entries = [new GatewayBatchEntry { DeviceApiId = "dev1", DeviceApiKey = token, Type = GatewayEntryType.CommandAck, Payload = payload }]
        });

        var result = Assert.Single(Assert.IsType<GatewayBatchResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result).Value).Results);
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task Batch_TokenSignedForAnotherDevice_RejectsEntryWithoutForwarding()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "gateway1", IsGateway = true, TenantID = 1 };
        var device = new Device { IDDevice = 2, ApiId = "dev1", ApiKey = "realKey", TenantID = 1 };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("gateway1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.DeviceGetByApiIdAsync("dev1")).ReturnsAsync(device);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });

        // Minted for a different device's ApiId/ApiKey - must not verify against dev1's.
        string token = GatewayDeviceToken.Issue("dev2", "someOtherKey");
        var controller = NewController("gateway1");
        var response = await controller.Batch(new GatewayBatchRequest
        {
            Entries = [new GatewayBatchEntry { DeviceApiId = "dev1", DeviceApiKey = token, Type = GatewayEntryType.Event, Payload = EmptyPayload() }]
        });

        var result = Assert.Single(Assert.IsType<GatewayBatchResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result).Value).Results);
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        // Strict mock: an un-set-up EventDevicePushAsync call would throw, proving the entry was never forwarded.
    }

    [Fact]
    public async Task DeviceMappingAdd_CallerFromDifferentTenantThanGateway_Returns403_NeverCallsRepo()
    {
        _repo.Setup(r => r.DeviceGetByIdAsync(1)).ReturnsAsync(new Device { IDDevice = 1, IsGateway = true, TenantID = 1 });

        var controller = NewJwtController();
        SetCallerRoles(controller, 2, RoleNames.TenantDevice); // caller manages organization 2, gateway belongs to organization 1
        var result = await controller.DeviceMappingAdd(new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "ABCDEF0123456789", IDDevice = 5 });

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        // Strict mock: an un-set-up GatewayDeviceMappingAddAsync call would throw, proving no mapping was attempted.
    }

    [Fact]
    public async Task DeviceMappingAdd_GlobalAdminFromDifferentTenant_PassesGatewaysOwnTenantId_NotCallersOwn()
    {
        _repo.Setup(r => r.DeviceGetByIdAsync(1)).ReturnsAsync(new Device { IDDevice = 1, IsGateway = true, TenantID = 1 });
        _repo.Setup(r => r.GatewayDeviceMappingAddAsync(1, "ABCDEF0123456789", 5, 1)).ReturnsAsync(true);

        var controller = NewJwtController();
        // GlobalAdmin's own organization (0) legitimately crosses the gateway-ownership check, but the gateway's OWN organization (1) - not the caller's - must be what's passed down for the device-organization guard.
        SetCallerRoles(controller, 0, RoleNames.GlobalAdmin);
        var result = await controller.DeviceMappingAdd(new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "abcdef0123456789", IDDevice = 5 });

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.GatewayDeviceMappingAddAsync(1, "ABCDEF0123456789", 5, 1), Times.Once);
    }

    [Fact]
    public async Task DeviceMappingGetAll_CallerFromDifferentTenantThanGateway_Returns403()
    {
        _repo.Setup(r => r.DeviceGetByIdAsync(1)).ReturnsAsync(new Device { IDDevice = 1, IsGateway = true, TenantID = 1 });

        var controller = NewJwtController();
        SetCallerRoles(controller, 2, RoleNames.TenantDevice);
        var result = await controller.DeviceMappingGetAll(1);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task DeviceMappingDelete_CallerFromDifferentTenantThanGateway_Returns403_NeverCallsRepo()
    {
        _repo.Setup(r => r.DeviceGetByIdAsync(1)).ReturnsAsync(new Device { IDDevice = 1, IsGateway = true, TenantID = 1 });

        var controller = NewJwtController();
        SetCallerRoles(controller, 2, RoleNames.TenantDevice);
        var result = await controller.DeviceMappingDelete(idGatewayDeviceMapping: 9, idGatewayDevice: 1);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    /// A LoRaGatewayEnabled device (not IsGateway) must be authorized the same as a classic Agrumy.Gateway.
    [Fact]
    public async Task RelayUplink_LoRaGatewayEnabledDevice_NotIsGateway_IsAuthorized()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", IsGateway = false, LoRaGatewayEnabled = true, TenantID = 1 };
        var mappedDevice = new Device { IDDevice = 2, ApiId = "dev2", TenantID = 1, LoRaPrivateKeyHex = Convert.ToHexString(TestLoRaKey) };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(
            [new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "42", IDDevice = 2 }]);
        _repo.Setup(r => r.DeviceGetByIdAsync(2)).ReturnsAsync(mappedDevice);
        _repo.Setup(r => r.DeviceLoRaSessionAcceptAsync(2, TestBootNonce, 1u)).ReturnsAsync(true);
        _repo.Setup(r => r.EventDevicePushAsync(2, 1, DeviceEventType.NoInternet, "relayed")).ReturnsAsync(true);

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest
        {
            SourceAddress = 42,
            Payload = EncryptForWire(TestLoRaKey, TestBootNonce, 1, "{\"t\":\"event\",\"EventType\":\"NoInternet\",\"Message\":\"relayed\"}"),
        });

        var result = Assert.IsType<GatewayBatchEntryResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    /// The node's own JSON payload can't carry its LoRa signal quality (only the receiving gateway knows it), so RunSensorDataAsync must stamp it on from the relay request instead.
    [Fact]
    public async Task RelayUplink_SensorDataEntry_AttachesGatewayRssiAndSnr()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", LoRaGatewayEnabled = true, TenantID = 1 };
        var mappedDevice = new Device { IDDevice = 2, ApiId = "dev2", TenantID = 1, LoRaPrivateKeyHex = Convert.ToHexString(TestLoRaKey) };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(
            [new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "42", IDDevice = 2 }]);
        _repo.Setup(r => r.DeviceGetByIdAsync(2)).ReturnsAsync(mappedDevice);
        _repo.Setup(r => r.DeviceLoRaSessionAcceptAsync(2, TestBootNonce, 1u)).ReturnsAsync(true);
        List<SensorDataPushReading>? pushed = null;
        _repo.Setup(r => r.SensorDataPushAsync(It.IsAny<IReadOnlyList<SensorDataPushReading>>(), 2, 1, null, null))
            .Callback<IReadOnlyList<SensorDataPushReading>, int, int, int?, int?>((readings, _, _, _, _) => pushed = readings.ToList())
            .Returns(Task.CompletedTask);

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest
        {
            SourceAddress = 42,
            Payload = EncryptForWire(TestLoRaKey, TestBootNonce, 1, "{\"t\":\"sensor\",\"d\":[{\"temperature\":21.5}]}"),
            Rssi = -87,
            Snr = 6,
        });

        Assert.True(Assert.IsType<GatewayBatchEntryResult>(Assert.IsType<OkObjectResult>(response.Result).Value).Success);
        var reading = Assert.Single(pushed!);
        Assert.Equal(21.5, reading.Temperature);
        Assert.Equal(-87, reading.LoRaRssiDbm);
        Assert.Equal(6, reading.LoRaSnrDb);
        Assert.Null(reading.WifiRssiDbm); // never set by a LoRa-only node
    }

    /// A replayed (bootNonce, counter) pair must 409 - the guarded UPDATE's own WHERE clause is the check (DeviceLoRaSessionAcceptAsync).
    [Fact]
    public async Task RelayUplink_ReplayedSession_Returns409()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", LoRaGatewayEnabled = true, TenantID = 1 };
        var mappedDevice = new Device { IDDevice = 2, ApiId = "dev2", TenantID = 1, LoRaPrivateKeyHex = Convert.ToHexString(TestLoRaKey) };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(
            [new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "42", IDDevice = 2 }]);
        _repo.Setup(r => r.DeviceGetByIdAsync(2)).ReturnsAsync(mappedDevice);
        _repo.Setup(r => r.DeviceLoRaSessionAcceptAsync(2, TestBootNonce, 1u)).ReturnsAsync(false);

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest
        {
            SourceAddress = 42,
            Payload = EncryptForWire(TestLoRaKey, TestBootNonce, 1, "{\"t\":\"event\"}"),
        });

        // Strict mock: an un-set-up EventDevicePushAsync/dispatch call would throw, proving the replay was rejected before any dispatch.
        Assert.Equal(409, Assert.IsType<GatewayBatchEntryResult>(Assert.IsType<OkObjectResult>(response.Result).Value).StatusCode);
    }

    /// No transition window - a frame in the retired counter-based wire format (leading 0x00) is malformed, period. Strict mock proves neither DeviceLoRaSessionAcceptAsync nor SensorDataPushAsync is ever reached.
    [Fact]
    public async Task RelayUplink_LegacyCounterShapedFrame_RejectedAsMalformed_NoSessionOrSensorRowWritten()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", LoRaGatewayEnabled = true, TenantID = 1 };
        var mappedDevice = new Device { IDDevice = 2, ApiId = "dev2", TenantID = 1, LoRaPrivateKeyHex = Convert.ToHexString(TestLoRaKey) };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(
            [new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "42", IDDevice = 2 }]);
        _repo.Setup(r => r.DeviceGetByIdAsync(2)).ReturnsAsync(mappedDevice);
        byte[] legacyShapedFrame = { 0x00, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest
        {
            SourceAddress = 42,
            Payload = Convert.ToBase64String(legacyShapedFrame),
        });

        // Strict mock: an un-set-up DeviceLoRaSessionAcceptAsync/SensorDataPushAsync call would throw, proving neither ran.
        Assert.Equal(401, Assert.IsType<GatewayBatchEntryResult>(Assert.IsType<OkObjectResult>(response.Result).Value).StatusCode);
    }

    [Fact]
    public async Task RelayUplink_UnmappedAddress_Returns404_NeverDispatches()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", LoRaGatewayEnabled = true, TenantID = 1 };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(new List<GatewayDeviceMapping>());

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest { SourceAddress = 99, Payload = "irrelevant" });

        // Strict mock: an un-set-up DeviceGetByIdAsync/dispatch call would throw, proving nothing was forwarded for an unmapped address.
        Assert.Equal(404, Assert.IsType<GatewayBatchEntryResult>(Assert.IsType<OkObjectResult>(response.Result).Value).StatusCode);
    }

    [Fact]
    public async Task RelayUplink_DeviceHasNoLoRaKeyProvisioned_Returns401_NeverDispatches()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", LoRaGatewayEnabled = true, TenantID = 1 };
        var mappedDevice = new Device { IDDevice = 2, ApiId = "dev2", TenantID = 1 }; // LoRaPrivateKeyHex left null - not yet provisioned
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(
            [new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "42", IDDevice = 2 }]);
        _repo.Setup(r => r.DeviceGetByIdAsync(2)).ReturnsAsync(mappedDevice);

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest { SourceAddress = 42, Payload = "irrelevant" });

        // Strict mock: an un-set-up DeviceLoRaSessionAcceptAsync/dispatch call would throw, proving nothing further ran.
        Assert.Equal(401, Assert.IsType<GatewayBatchEntryResult>(Assert.IsType<OkObjectResult>(response.Result).Value).StatusCode);
    }

    [Fact]
    public async Task RelayUplink_WrongKey_FailsAuthTagCheck_Returns401()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", LoRaGatewayEnabled = true, TenantID = 1 };
        var mappedDevice = new Device { IDDevice = 2, ApiId = "dev2", TenantID = 1, LoRaPrivateKeyHex = Convert.ToHexString(TestLoRaKey) };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(
            [new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "42", IDDevice = 2 }]);
        _repo.Setup(r => r.DeviceGetByIdAsync(2)).ReturnsAsync(mappedDevice);
        byte[] wrongKey = new byte[32]; // all zeros - guaranteed different from TestLoRaKey

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest
        {
            SourceAddress = 42,
            Payload = EncryptForWire(wrongKey, TestBootNonce, 1, "{\"t\":\"event\"}"),
        });

        Assert.Equal(401, Assert.IsType<GatewayBatchEntryResult>(Assert.IsType<OkObjectResult>(response.Result).Value).StatusCode);
    }

    /// The outer [EnableRateLimiting("device-data")] is keyed by the GATEWAY's IP, shared by every leaf relayed through it; this per-leaf ceiling is a separate guard so one noisy node can't starve its siblings.
    [Fact]
    public async Task RelayUplink_LeafAtItsOwnPerLeafRateLimit_Returns429_NeverDecrypts()
    {
        var gateway = new Device { IDDevice = 1, ApiId = "node1", LoRaGatewayEnabled = true, TenantID = 1 };
        var mappedDevice = new Device { IDDevice = 2, ApiId = "dev2", TenantID = 1, LoRaPrivateKeyHex = Convert.ToHexString(TestLoRaKey) };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(gateway);
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { GatewayEnabled = true });
        _repo.Setup(r => r.GatewayDeviceMappingsGetAsync(1)).ReturnsAsync(
            [new GatewayDeviceMapping { IDGatewayDevice = 1, DevEUI = "42", IDDevice = 2 }]);
        _repo.Setup(r => r.DeviceGetByIdAsync(2)).ReturnsAsync(mappedDevice);
        _cache.Setup(c => c.GetAsync<GatewayApiController.RelayRateCounter>("relay-rate:2"))
            .ReturnsAsync(new GatewayApiController.RelayRateCounter { WindowStart = DateTimeOffset.UtcNow, Count = 20 });

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest
        {
            SourceAddress = 42,
            Payload = EncryptForWire(TestLoRaKey, TestBootNonce, 1, "{\"t\":\"event\"}"),
        });

        // Strict mock: no DeviceLoRaSessionAcceptAsync/dispatch setup, proving this leaf's request never reached decryption.
        Assert.Equal(429, Assert.IsType<ObjectResult>(response.Result).StatusCode);
    }

    [Fact]
    public async Task RelayUplink_DeviceNeitherGatewayNorLoRaGatewayEnabled_Returns403()
    {
        var caller = new Device { IDDevice = 1, ApiId = "node1", IsGateway = false, LoRaGatewayEnabled = false, TenantID = 1 };
        _repo.Setup(r => r.DeviceGetByApiIdAsync("node1")).ReturnsAsync(caller);

        var controller = NewController("node1");
        var response = await controller.RelayUplink(new GatewayRelayUplinkRequest { SourceAddress = 42, Payload = "{\"t\":\"event\"}" });

        Assert.Equal(403, Assert.IsType<ObjectResult>(response.Result).StatusCode);
    }
}
