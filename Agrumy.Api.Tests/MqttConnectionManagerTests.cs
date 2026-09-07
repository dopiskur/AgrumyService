using api.Commands;
using MQTTnet;
using MQTTnet.Client;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises the connect/reconnect/publish logic against a mocked IMqttClient - the real network transport (like MqttCommandPublisherTests' own network call) is untestable without a broker.
public class MqttConnectionManagerTests
{
    private readonly Mock<IMqttClient> _client = new(MockBehavior.Strict);

    private static MqttApplicationMessage Message() => new MqttApplicationMessageBuilder().WithTopic("t").WithPayload("p").Build();

    [Fact]
    public async Task FirstPublish_NotConnected_ConnectsThenPublishes()
    {
        _client.SetupGet(c => c.IsConnected).Returns(false);
        _client.Setup(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MqttClientConnectResult?)null!);
        _client.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MqttClientPublishResult?)null!);

        var manager = new MqttConnectionManager(_client.Object);
        await manager.PublishAsync("broker.example.com", 1883, null, null, Message(), CancellationToken.None);

        _client.Verify(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _client.Verify(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SecondPublish_SameBroker_AlreadyConnected_ReusesConnection_DoesNotReconnect()
    {
        // First call establishes the connection (and its fingerprint); IsConnected flips true from then on, same as the real client after a successful ConnectAsync.
        _client.SetupSequence(c => c.IsConnected).Returns(false).Returns(true);
        _client.Setup(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MqttClientConnectResult?)null!);
        _client.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MqttClientPublishResult?)null!);

        var manager = new MqttConnectionManager(_client.Object);
        await manager.PublishAsync("broker.example.com", 1883, null, null, Message(), CancellationToken.None);
        await manager.PublishAsync("broker.example.com", 1883, null, null, Message(), CancellationToken.None);

        // Strict mock: DisconnectAsync was never set up - reusing an already-open connection to the same broker must not touch it, and ConnectAsync must not run a second time.
        _client.Verify(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _client.Verify(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BrokerSettingsChanged_ReconnectsBeforePublishing()
    {
        _client.SetupSequence(c => c.IsConnected)
            .Returns(false) // first publish: not connected yet
            .Returns(true); // second publish: still connected, but to the OLD broker - fingerprint mismatch must still trigger reconnect
        _client.Setup(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MqttClientConnectResult?)null!);
        _client.Setup(c => c.DisconnectAsync(It.IsAny<MqttClientDisconnectOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _client.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MqttClientPublishResult?)null!);

        var manager = new MqttConnectionManager(_client.Object);
        await manager.PublishAsync("broker-a.example.com", 1883, null, null, Message(), CancellationToken.None);
        await manager.PublishAsync("broker-b.example.com", 1883, null, null, Message(), CancellationToken.None);

        _client.Verify(c => c.DisconnectAsync(It.IsAny<MqttClientDisconnectOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _client.Verify(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Port8883_UsesTls()
    {
        MqttClientOptions? captured = null;
        _client.SetupGet(c => c.IsConnected).Returns(false);
        _client.Setup(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MqttClientOptions, CancellationToken>((o, _) => captured = o)
            .ReturnsAsync((MqttClientConnectResult?)null!);
        _client.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MqttClientPublishResult?)null!);

        var manager = new MqttConnectionManager(_client.Object);
        await manager.PublishAsync("broker.example.com", 8883, null, null, Message(), CancellationToken.None);

        Assert.True(captured!.ChannelOptions is MqttClientTcpOptions { TlsOptions.UseTls: true });
    }
}
