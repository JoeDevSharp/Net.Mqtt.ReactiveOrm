namespace Net.Mqtt.Infrastructure.Models
{
    /// <summary>
    /// Represents the legacy MQTT server connection options.
    /// </summary>
    public class MqttServerOptions
    {
        /// <summary>
        /// Gets or sets the MQTT client identifier.
        /// </summary>
        public string ClientId { get; set; } = $"mqttorm-{Guid.NewGuid()}";

        /// <summary>
        /// Gets or sets the MQTT server host name or IP address.
        /// </summary>
        public string Server { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the MQTT server port.
        /// </summary>
        public int Port { get; set; } = 1883;

        /// <summary>
        /// Gets or sets the optional broker user name.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the optional broker password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets whether the legacy connection uses TLS.
        /// </summary>
        public bool UseTls { get; set; } = false;
    }
}
