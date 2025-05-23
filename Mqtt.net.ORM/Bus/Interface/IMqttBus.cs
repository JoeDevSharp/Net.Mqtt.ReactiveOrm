namespace Mqtt.net.ORM.Bus.Interfaces
{

    /// <summary>
    /// Defines a strongly‑typed MQTT message bus.
    /// </summary>
    public interface IMqttBus
    {
        /// <summary>
        /// Ensures the underlying MQTT client is connected.
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Publishes a message instance to its resolved topic.
        /// </summary>
        /// <typeparam name="T">Message type decorated with [MqttTopic]</typeparam>
        /// <param name="message">The message object to serialize and publish.</param>
        /// <param name="parameters">
        /// Optional template values for topics with placeholders,
        /// e.g. new { deviceId = "123" } for "devices/{deviceId}/status".
        /// </param>
        Task PublishAsync<T>(object message);

        /// <summary>
        /// Subscribes a handler to a parameterized topic resolved from <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Message type decorated with [MqttTopic]</typeparam>
        /// <param name="handler">Async handler invoked on incoming messages.</param>
        /// <param name="parameters">
        /// Anonymous object whose property names match the template placeholders,
        /// e.g. new { deviceId = "+" } to subscribe with wildcard.
        /// </param>
        /// <param name="overwrite">
        /// If true, replaces any existing handler for the same topic.
        /// Defaults to false (throws on duplicate).
        /// </param>
        Task SubscribeAsync<T>(Func<T, Task> handler);

        /// <summary>
        /// Unsubscribes the handler and cancels the subscription for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Message type decorated with [MqttTopic]</typeparam>
        Task UnsubscribeAsync<T>();
    }
}
