namespace Net.Mqtt.ReactiveOrm.Models
{
    /// <summary>
    /// Representa las opciones de configuración para la conexión con un servidor MQTT.
    /// </summary>
    public class MqttServerOptions
    {
        /// <summary>
        /// Identificador del cliente MQTT. Se genera uno automáticamente si no se especifica.
        /// </summary>
        public string ClientId { get; set; } = $"mqttorm-{Guid.NewGuid()}";

        /// <summary>
        /// Dirección del servidor MQTT (hostname o IP).
        /// </summary>
        public string Server { get; set; } = "localhost";

        /// <summary>
        /// Puerto del servidor MQTT. El valor por defecto es 1883.
        /// </summary>
        public int Port { get; set; } = 1883;

        /// <summary>
        /// Nombre de usuario para autenticación con el broker (opcional).
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Contraseña para autenticación con el broker (opcional).
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Indica si se debe usar TLS/SSL para la conexión.
        /// </summary>
        public bool UseTls { get; set; } = false;
    }
}
