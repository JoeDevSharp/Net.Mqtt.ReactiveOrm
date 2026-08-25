using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Net.Mqtt.Infrastructure.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Net.Mqtt.Infrastructure.Models;

/// <summary>Defines the network transport used for MQTT.</summary>
public enum MqttTransport
{
    /// <summary>Uses a direct TCP connection.</summary>
    Tcp,
    /// <summary>Uses an MQTT WebSocket connection.</summary>
    WebSocket
}

/// <summary>Represents mqtt reactive orm options.</summary>
public sealed class MqttReactiveOrmOptions
{
    /// <summary>Gets or sets protocol version.</summary>
    public MqttProtocolVersion ProtocolVersion { get; set; } = MqttProtocolVersion.V500;
    /// <summary>Gets or sets server.</summary>
    public string Server { get; set; } = "localhost";
    /// <summary>Gets or sets port.</summary>
    public int Port { get; set; } = 1883;
    /// <summary>Gets or sets transport.</summary>
    public MqttTransport Transport { get; set; } = MqttTransport.Tcp;
    /// <summary>Gets or sets web socket uri.</summary>
    public string? WebSocketUri { get; set; }
    /// <summary>Gets or sets client id.</summary>
    public required string ClientId { get; set; }
    /// <summary>Gets or sets keep alive.</summary>
    public TimeSpan KeepAlive { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Gets or sets timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Gets or sets maximum packet size.</summary>
    public uint MaximumPacketSize { get; set; } = 1024 * 1024;
    /// <summary>Gets or sets receive maximum.</summary>
    public ushort ReceiveMaximum { get; set; } = 32;
    /// <summary>Gets session.</summary>
    public MqttSessionOptions Session { get; } = new();
    /// <summary>Gets reconnect.</summary>
    public MqttReconnectOptions Reconnect { get; } = new();
    /// <summary>Gets last will.</summary>
    public MqttLastWillOptions LastWill { get; } = new();
    /// <summary>Gets security.</summary>
    public MqttSecurityOptions Security { get; } = new();

    internal async ValueTask<MqttClientOptions> BuildClientOptionsAsync(CancellationToken cancellationToken = default)
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

        if (Security.MutualTls is { } mtls)
        {
            var certificate = await mtls.ClientCertificateProvider.GetCertificateAsync(cancellationToken).ConfigureAwait(false);
            mtls.ValidateClientCertificate(certificate);
            builder.WithTlsOptions(new MqttClientTlsOptions
            {
                UseTls = true,
                TargetHost = mtls.ExpectedServerName ?? Server,
                SslProtocol = SslProtocols.Tls12 | SslProtocols.Tls13,
                AllowUntrustedCertificates = false,
                IgnoreCertificateChainErrors = false,
                IgnoreCertificateRevocationErrors = false,
                ClientCertificatesProvider = new DefaultMqttCertificatesProvider([certificate])
            });
        }

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

    /// <summary>Validates the validate operation.</summary>
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
        Security.Validate();
    }

    internal static uint ToSeconds(TimeSpan value) => checked((uint)Math.Clamp(value.TotalSeconds, 0, uint.MaxValue));
    internal static byte[] CreateUnavailableCloudEvent(string clientId) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        specversion = "1.0", id = Guid.NewGuid().ToString("N"), source = $"urn:mqtt-client:{clientId}",
        type = "com.netmqtt.service.availability.v1", time = DateTimeOffset.UtcNow, datacontenttype = "application/json",
        data = new { status = "UNAVAILABLE" }
    });
}

/// <summary>Represents mqtt security options.</summary>
public sealed class MqttSecurityOptions
{
    /// <summary>Gets mutual tls.</summary>
    public MutualTlsOptions? MutualTls { get; private set; }
    /// <summary>Configures the use mutual tls operation.</summary>
    public void UseMutualTls(Action<MutualTlsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new MutualTlsOptions();
        configure(options);
        options.Validate();
        MutualTls = options;
    }
    internal void Validate() => MutualTls?.Validate();
}

/// <summary>Represents mutual tls options.</summary>
public sealed class MutualTlsOptions
{
    /// <summary>Gets or sets client certificate provider.</summary>
    public IClientCertificateProvider ClientCertificateProvider { get; set; } = null!;
    /// <summary>Gets or sets require trusted server certificate.</summary>
    public bool RequireTrustedServerCertificate { get; set; } = true;
    /// <summary>Gets or sets check certificate revocation.</summary>
    public bool CheckCertificateRevocation { get; set; } = true;
    /// <summary>Gets or sets expected server name.</summary>
    public string? ExpectedServerName { get; set; }
    /// <summary>Gets or sets expected client identity.</summary>
    public string? ExpectedClientIdentity { get; set; }
    /// <summary>Gets or sets expiration warning threshold.</summary>
    public TimeSpan ExpirationWarningThreshold { get; set; } = TimeSpan.FromDays(30);
    /// <summary>Gets or sets expiration check interval.</summary>
    public TimeSpan ExpirationCheckInterval { get; set; } = TimeSpan.FromHours(1);

    internal void Validate()
    {
        if (ClientCertificateProvider is null) throw new InvalidOperationException("A client certificate provider is required for mTLS.");
        if (!RequireTrustedServerCertificate)
            throw new InvalidOperationException("Untrusted server certificates are forbidden.");
        if (!CheckCertificateRevocation)
            throw new InvalidOperationException("Certificate revocation checks cannot be disabled.");
        ArgumentException.ThrowIfNullOrWhiteSpace(ExpectedServerName);
        if (ExpirationWarningThreshold < TimeSpan.Zero || ExpirationCheckInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ExpirationCheckInterval));
    }

    internal void ValidateClientCertificate(X509Certificate2 certificate)
    {
        var now = DateTimeOffset.UtcNow;
        if (!certificate.HasPrivateKey) throw new InvalidOperationException("The mTLS client certificate has no private key.");
        if (now < certificate.NotBefore || now > certificate.NotAfter)
            throw new InvalidOperationException("The mTLS client certificate is not currently valid.");
        if (string.IsNullOrWhiteSpace(ExpectedClientIdentity)) return;
        var dns = certificate.GetNameInfo(X509NameType.DnsName, false);
        var simple = certificate.GetNameInfo(X509NameType.SimpleName, false);
        if (!string.Equals(ExpectedClientIdentity, dns, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ExpectedClientIdentity, simple, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Certificate identity '{dns}'/'{simple}' does not match '{ExpectedClientIdentity}'.");
    }
}

/// <summary>Represents mqtt session options.</summary>
public sealed class MqttSessionOptions
{
    /// <summary>Gets or sets clean start.</summary>
    public bool CleanStart { get; set; }
    /// <summary>Gets or sets expiry.</summary>
    public TimeSpan Expiry { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>Represents mqtt reconnect options.</summary>
public sealed class MqttReconnectOptions
{
    /// <summary>Gets or sets initial delay.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>Gets or sets maximum delay.</summary>
    public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>Gets or sets multiplier.</summary>
    public double Multiplier { get; set; } = 2;
    /// <summary>Gets or sets jitter ratio.</summary>
    public double JitterRatio { get; set; } = .2;
    /// <summary>Gets or sets maximum attempts.</summary>
    public int? MaximumAttempts { get; set; }
    /// <summary>Gets or sets maximum duration.</summary>
    public TimeSpan? MaximumDuration { get; set; }
    /// <summary>Configures the use exponential backoff operation.</summary>
    public void UseExponentialBackoff(TimeSpan? initialDelay = null, TimeSpan? maximumDelay = null)
    {
        InitialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        MaximumDelay = maximumDelay ?? TimeSpan.FromMinutes(1);
        Multiplier = 2;
    }
}

/// <summary>Represents mqtt last will options.</summary>
public sealed class MqttLastWillOptions
{
    /// <summary>Gets or sets enabled.</summary>
    public bool Enabled { get; set; }
    /// <summary>Gets or sets topic.</summary>
    public string? Topic { get; set; }
    /// <summary>Gets or sets payload.</summary>
    public byte[]? Payload { get; set; }
    /// <summary>Gets or sets retain.</summary>
    public bool Retain { get; set; } = true;
    /// <summary>Gets or sets message expiry.</summary>
    public TimeSpan MessageExpiry { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>Configures the use service unavailable cloud event operation.</summary>
    public void UseServiceUnavailableCloudEvent(string? topic = null)
    {
        Enabled = true;
        Topic = topic;
    }
}
