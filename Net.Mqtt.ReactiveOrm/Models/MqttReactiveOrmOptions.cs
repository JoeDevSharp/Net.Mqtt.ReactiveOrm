using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using System.Text.Json;

namespace Net.Mqtt.ReactiveOrm.Models;

public enum MqttTransport { Tcp, WebSocket }

public sealed class MqttReactiveOrmOptions
{
    public MqttProtocolVersion ProtocolVersion { get; set; } = MqttProtocolVersion.V500;
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public MqttTransport Transport { get; set; } = MqttTransport.Tcp;
    public string? WebSocketUri { get; set; }
    public required string ClientId { get; set; }
    public TimeSpan KeepAlive { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public uint MaximumPacketSize { get; set; } = 1024 * 1024;
    public ushort ReceiveMaximum { get; set; } = 32;
    public MqttSessionOptions Session { get; } = new();
    public MqttReconnectOptions Reconnect { get; } = new();
    public MqttLastWillOptions LastWill { get; } = new();

    internal MqttClientOptions BuildClientOptions()
    {
        Validate();
        var builder = new MqttClientOptionsBuilder().WithClientId(ClientId).WithProtocolVersion(ProtocolVersion)
            .WithKeepAlivePeriod(KeepAlive).WithTimeout(Timeout);
        if (Transport == MqttTransport.Tcp) builder.WithTcpServer(Server, Port);
        else builder.WithWebSocketServer(o => o.WithUri(WebSocketUri!));

        if (ProtocolVersion == MqttProtocolVersion.V500)
            builder.WithCleanStart(Session.CleanStart).WithSessionExpiryInterval(ToSeconds(Session.Expiry))
                .WithMaximumPacketSize(MaximumPacketSize).WithReceiveMaximum(ReceiveMaximum);
        else
            builder.WithCleanSession(false);

        var result = builder.Build();
        if (LastWill.Enabled)
        {
            result.WillTopic = LastWill.Topic ?? $"services/{ClientId}/availability";
            result.WillPayload = LastWill.Payload ?? CreateUnavailableCloudEvent(ClientId);
            result.WillRetain = LastWill.Retain;
            result.WillQualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce;
            if (ProtocolVersion == MqttProtocolVersion.V500)
            {
                result.WillContentType = "application/cloudevents+json";
                result.WillMessageExpiryInterval = ToSeconds(LastWill.MessageExpiry);
            }
        }
        return result;
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        if (ClientId.Length > 65535) throw new ArgumentOutOfRangeException(nameof(ClientId));
        if (ProtocolVersion == MqttProtocolVersion.V311 && ClientId.Length > 23)
            throw new ArgumentException("An MQTT 3.1.1 ClientId cannot exceed 23 characters in compatibility mode.", nameof(ClientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(Server);
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (KeepAlive <= TimeSpan.Zero || Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(KeepAlive));
        if (MaximumPacketSize == 0 || ReceiveMaximum == 0) throw new ArgumentOutOfRangeException(nameof(MaximumPacketSize));
        if (Transport == MqttTransport.WebSocket && string.IsNullOrWhiteSpace(WebSocketUri))
            throw new ArgumentException("WebSocketUri is required for WebSocket transport.");
    }

    internal static uint ToSeconds(TimeSpan value) => checked((uint)Math.Clamp(value.TotalSeconds, 0, uint.MaxValue));
    internal static byte[] CreateUnavailableCloudEvent(string clientId) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        specversion = "1.0", id = Guid.NewGuid().ToString("N"), source = $"urn:mqtt-client:{clientId}",
        type = "com.netmqtt.service.availability.v1", time = DateTimeOffset.UtcNow, datacontenttype = "application/json",
        data = new { status = "UNAVAILABLE" }
    });
}

public sealed class MqttSessionOptions
{
    public bool CleanStart { get; set; }
    public TimeSpan Expiry { get; set; } = TimeSpan.FromHours(24);
}

public sealed class MqttReconnectOptions
{
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromMinutes(1);
    public double Multiplier { get; set; } = 2;
    public double JitterRatio { get; set; } = .2;
    public int? MaximumAttempts { get; set; }
    public TimeSpan? MaximumDuration { get; set; }
    public void UseExponentialBackoff(TimeSpan? initialDelay = null, TimeSpan? maximumDelay = null)
    {
        InitialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        MaximumDelay = maximumDelay ?? TimeSpan.FromMinutes(1);
        Multiplier = 2;
    }
}

public sealed class MqttLastWillOptions
{
    public bool Enabled { get; set; }
    public string? Topic { get; set; }
    public byte[]? Payload { get; set; }
    public bool Retain { get; set; } = true;
    public TimeSpan MessageExpiry { get; set; } = TimeSpan.FromMinutes(5);
    public void UseServiceUnavailableCloudEvent(string? topic = null)
    {
        Enabled = true;
        Topic = topic;
    }
}
