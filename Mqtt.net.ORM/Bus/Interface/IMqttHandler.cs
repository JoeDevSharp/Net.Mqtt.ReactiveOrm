namespace Mqtt.net.ORM.Bus.Interfaces
{
    /// <summary>
    /// Defines a handler for incoming MQTT messages of type T.
    /// </summary>
    /// <typeparam name="T">The message type this handler is responsible for.</typeparam>
    public interface IMqttHandler<T>
    {
        /// <summary>
        /// Handles a received MQTT message.
        /// </summary>
        /// <param name="message">The message object deserialized from the MQTT payload.</param>
        Task HandleAsync(T message);
    }
}
