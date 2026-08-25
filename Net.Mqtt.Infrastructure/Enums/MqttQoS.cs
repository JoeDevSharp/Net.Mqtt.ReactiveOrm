namespace Net.Mqtt.Infrastructure.Enums;

/// <summary>Defines mqtt qo s.</summary>
public enum MqttQoS
{
    /// <summary>Gets at most once.</summary>
    AtMostOnce = 0,
    /// <summary>Gets at least once.</summary>
    AtLeastOnce = 1,
    /// <summary>Gets exactly once.</summary>
    ExactlyOnce = 2
}
