using Mqtt.net.ORM.Bus;
using Mqtt.net.ORM.Models;
using MQTTnet;
using System.Reflection;

namespace Mqtt.net.ORM
{
    public class MqttContext
    {
        /// <summary>
        /// Instancia interna de opciones para almacenar los valores de configuración del servidor MQTT.
        /// </summary>
        private MqttServerOptions Options { get; } = new();

        public MqttBus MqttBus;

        public MqttContext(string host, int port = 1883)
        {
            Options.Server = host;
            Options.Port = port;

            var clientFactory = new MqttClientFactory();
            var mqttClient = clientFactory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(Options.Server, Options.Port)
                .WithClientId(Options.ClientId)
                .WithCredentials(Options.Username, Options.Password)
                .Build();

            var serializer = new MqttSerializer();

            MqttBus = new MqttBus(mqttClient, options, serializer);

            InitializeTopicSets();
        }

        private void InitializeTopicSets()
        {
            var topicSetType = typeof(TopicSet<>);

            // Busca todas las propiedades públicas de instancia que sean TopicSet<T>
            var props = this.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == topicSetType);

            foreach (var prop in props)
            {
                var genericArg = prop.PropertyType.GetGenericArguments()[0];
                var constructed = typeof(TopicSet<>).MakeGenericType(genericArg);
                var instance = Activator.CreateInstance(constructed, MqttBus);
                prop.SetValue(this, instance);
            }
        }

        /// <summary>
        /// Configura la dirección y el puerto del servidor MQTT.
        /// </summary>
        /// <param name="host">Dirección del servidor MQTT.</param>
        /// <param name="port">Puerto del servidor MQTT (por defecto 1883).</param>
        /// <returns>Instancia de <see cref="MqttContext"/> para encadenar llamadas.</returns>
        public MqttContext WithServer(string host, int port = 1883)
        {
            Options.Server = host;
            Options.Port = port;
            return this;
        }

        /// <summary>
        /// Configura el identificador del cliente MQTT.
        /// </summary>
        /// <param name="clientId">Identificador del cliente.</param>
        /// <returns>Instancia de <see cref="MqttContext"/> para encadenar llamadas.</returns>
        public MqttContext WithClientId(string clientId)
        {
            Options.ClientId = clientId;
            return this;
        }

        /// <summary>
        /// Configura las credenciales de usuario y contraseña para el cliente MQTT.
        /// </summary>
        /// <param name="username">Nombre de usuario.</param>
        /// <param name="password">Contraseña.</param>
        /// <returns>Instancia de <see cref="MqttContext"/> para encadenar llamadas.</returns>
        public MqttContext WithCredentials(string username, string password)
        {
            Options.Username = username;
            Options.Password = password;
            return this;
        }

        /// <summary>
        /// Habilita o deshabilita el uso de TLS para la conexión MQTT.
        /// </summary>
        /// <param name="enabled">Indica si TLS está habilitado (por defecto true).</param>
        /// <returns>Instancia de <see cref="MqttContext"/> para encadenar llamadas.</returns>
        public MqttContext UseTls(bool enabled = true)
        {
            Options.UseTls = enabled;
            return this;
        }
    }
}
