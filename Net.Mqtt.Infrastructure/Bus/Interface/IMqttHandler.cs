namespace Net.Mqtt.Infrastructure.Bus.Interfaces
{
    /// <summary>
    /// Defines a handler for incoming MQTT messages of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The message type processed by the handler.</typeparam>
    public interface IMqttHandler<T>
    {
        /// <summary>
        /// Processes a received MQTT message.
        /// </summary>
        /// <param name="message">The message deserialized from the MQTT payload.</param>
        Task HandleAsync(T message);
    }
}
