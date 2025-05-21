using MqttORM.Models;

namespace MqttORM.Configuration
{
    /// <summary>
    /// Builder para configurar las opciones de MqttOrmOptions.
    /// Permite establecer los parámetros de conexión y autenticación para el cliente MQTT.
    /// </summary>
    public class MqttOrmBuilder
    {
        /// <summary>
        /// Instancia interna de opciones para almacenar los valores de configuración.
        /// </summary>
        internal MqttOrmOptions Options { get; } = new();

        /// <summary>
        /// Establece el host y el puerto del servidor MQTT.
        /// </summary>
        /// <param name="host">Dirección del servidor MQTT.</param>
        /// <param name="port">Puerto del servidor MQTT (por defecto 1883).</param>
        /// <returns>Instancia de <see cref="MqttOrmBuilder"/> para encadenar llamadas.</returns>
        public MqttOrmBuilder WithServer(string host, int port = 1883)
        {
            Options.Server = host;
            Options.Port = port;
            return this;
        }

        /// <summary>
        /// Establece el identificador del cliente MQTT.
        /// </summary>
        /// <param name="clientId">Identificador del cliente.</param>
        /// <returns>Instancia de <see cref="MqttOrmBuilder"/> para encadenar llamadas.</returns>
        public MqttOrmBuilder WithClientId(string clientId)
        {
            Options.ClientId = clientId;
            return this;
        }

        /// <summary>
        /// Establece las credenciales de usuario y contraseña para el cliente MQTT.
        /// </summary>
        /// <param name="username">Nombre de usuario.</param>
        /// <param name="password">Contraseña.</param>
        /// <returns>Instancia de <see cref="MqttOrmBuilder"/> para encadenar llamadas.</returns>
        public MqttOrmBuilder WithCredentials(string username, string password)
        {
            Options.Username = username;
            Options.Password = password;
            return this;
        }

        /// <summary>
        /// Habilita o deshabilita TLS para la conexión MQTT.
        /// </summary>
        /// <param name="enabled">Indica si TLS está habilitado (por defecto true).</param>
        /// <returns>Instancia de <see cref="MqttOrmBuilder"/> para encadenar llamadas.</returns>
        public MqttOrmBuilder UseTls(bool enabled = true)
        {
            Options.UseTls = enabled;
            return this;
        }
    }
}
