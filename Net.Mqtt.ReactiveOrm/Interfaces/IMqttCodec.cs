namespace Net.Mqtt.ReactiveOrm.Interfaces;

public interface IMqttCodec
{
    ReadOnlyMemory<byte> Encode<T>(T value);
    T Decode<T>(ReadOnlyMemory<byte> payload);
}
