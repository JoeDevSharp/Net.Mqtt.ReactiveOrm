using Net.Mqtt.Infrastructure.Enums;

namespace Net.Mqtt.Infrastructure.Attributes;

/// <summary>Maps a typed topic set to separate MQTT publication and subscription addresses.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class MqttTopicAttribute : Attribute
{
    /// <summary>Gets or sets publish topic.</summary>
    public string? PublishTopic { get; init; }
    /// <summary>Gets or sets subscribe filter.</summary>
    public required string SubscribeFilter { get; init; }
    /// <summary>Gets or sets qo s.</summary>
    public MqttQoS QoS { get; init; } = MqttQoS.AtMostOnce;
    /// <summary>Gets or sets retain.</summary>
    public bool Retain { get; init; }
    /// <summary>Gets or sets resolver type.</summary>
    public Type? ResolverType { get; init; }
}
