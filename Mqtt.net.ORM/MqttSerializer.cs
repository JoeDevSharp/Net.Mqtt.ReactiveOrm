using System.Text.Json;

namespace Mqtt.net.ORM
{
    /// <summary>
    /// Provides serialization and deserialization for MQTT messages.
    /// </summary>
    public class MqttSerializer
    {
        private readonly JsonSerializerOptions _options;

        /// <summary>
        /// Initializes a new instance of MqttSerializer with optional settings.
        /// </summary>
        /// <param name="options">Optional JsonSerializerOptions. If null, defaults are used.</param>
        public MqttSerializer(JsonSerializerOptions? options = null)
        {
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Serializes a message object to a UTF-8 JSON string.
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="message">The object to serialize</param>
        /// <returns>UTF-8 JSON string</returns>
        public string Serialize<T>(T message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return JsonSerializer.Serialize(message, _options);
        }

        /// <summary>
        /// Deserializes a JSON string to the given message type.
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="payload">The raw JSON string</param>
        /// <returns>The deserialized object</returns>
        public T Deserialize<T>(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                throw new ArgumentException("Payload must not be empty.", nameof(payload));

            return JsonSerializer.Deserialize<T>(payload, _options)
                   ?? throw new InvalidOperationException("Failed to deserialize payload.");
        }
    }
}
