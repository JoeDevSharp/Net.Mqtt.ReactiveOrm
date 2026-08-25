using Net.Mqtt.ReactiveOrm.Enums;

namespace Net.Mqtt.ReactiveOrm.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class MqttTopicAttribute : Attribute
{
    public string? PublishTopic { get; init; }
    public required string SubscribeFilter { get; init; }
    public MqttQoS QoS { get; init; } = MqttQoS.AtMostOnce;
    public bool Retain { get; init; }
    public Type? ResolverType { get; init; }
}
