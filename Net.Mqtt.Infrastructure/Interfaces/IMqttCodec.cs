namespace Net.Mqtt.Infrastructure.Interfaces;

/// <summary>Defines a general-purpose codec for legacy MQTT payloads.</summary>
public interface IMqttCodec
{
    /// <summary>Encodes a value as an MQTT payload.</summary>
    ReadOnlyMemory<byte> Encode<T>(T value);
    /// <summary>Decodes an MQTT payload into a value.</summary>
    T Decode<T>(ReadOnlyMemory<byte> payload);
}
