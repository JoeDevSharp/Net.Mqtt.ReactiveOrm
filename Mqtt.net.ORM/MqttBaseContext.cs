using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus;
using Mqtt.net.ORM.Bus.Interfaces;
using Mqtt.net.ORM.Models;
using MQTTnet;
using System.Reflection;

namespace Mqtt.net.ORM
{
    public class MqttBaseContext
    {
        /// <summary>
        /// Instancia interna de opciones para almacenar los valores de configuración del servidor MQTT.
        /// </summary>
        private MqttServerOptions Options { get; } = new();

        public MqttBus MqttBus;

        public MqttBaseContext(string host = "localhost", int port = 1883)
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
            var topicSetGenericType = typeof(TopicSet<>);

            var properties = GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p =>
                    p.PropertyType.IsGenericType &&
                    p.PropertyType.GetGenericTypeDefinition() == topicSetGenericType &&
                    p.GetCustomAttribute<TopicAttribute>() != null
                );

            foreach (var property in properties)
            {
                var topicAttr = property.GetCustomAttribute<TopicAttribute>();
                var genericArg = property.PropertyType.GetGenericArguments()[0];

                var constructedTopicSetType = topicSetGenericType.MakeGenericType(genericArg);

                // Busca un constructor que reciba (IMqttBus, TopicAttribute)
                var constructor = constructedTopicSetType.GetConstructor(new[] { typeof(IMqttBus), typeof(TopicAttribute) });
                if (constructor == null)
                {
                    throw new InvalidOperationException($"TopicSet<{genericArg.Name}> must have a constructor with (IMqttBus, TopicAttribute)");
                }

                var instance = constructor.Invoke(new object[] { MqttBus, topicAttr });

                property.SetValue(this, instance);
            }
        }


        /// <summary>
        /// Configura la dirección y el puerto del servidor MQTT.
        /// </summary>
        /// <param name="host">Dirección del servidor MQTT.</param>
        /// <param name="port">Puerto del servidor MQTT (por defecto 1883).</param>
        /// <returns>Instancia de <see cref="MqttBaseContext"/> para encadenar llamadas.</returns>
        public MqttBaseContext WithServer(string host, int port = 1883)
        {
            Options.Server = host;
            Options.Port = port;
            return this;
        }

        /// <summary>
        /// Configura el identificador del cliente MQTT.
        /// </summary>
        /// <param name="clientId">Identificador del cliente.</param>
        /// <returns>Instancia de <see cref="MqttBaseContext"/> para encadenar llamadas.</returns>
        public MqttBaseContext WithClientId(string clientId)
        {
            Options.ClientId = clientId;
            return this;
        }

        /// <summary>
        /// Configura las credenciales de usuario y contraseña para el cliente MQTT.
        /// </summary>
        /// <param name="username">Nombre de usuario.</param>
        /// <param name="password">Contraseña.</param>
        /// <returns>Instancia de <see cref="MqttBaseContext"/> para encadenar llamadas.</returns>
        public MqttBaseContext WithCredentials(string username, string password)
        {
            Options.Username = username;
            Options.Password = password;
            return this;
        }

        /// <summary>
        /// Habilita o deshabilita el uso de TLS para la conexión MQTT.
        /// </summary>
        /// <param name="enabled">Indica si TLS está habilitado (por defecto true).</param>
        /// <returns>Instancia de <see cref="MqttBaseContext"/> para encadenar llamadas.</returns>
        public MqttBaseContext UseTls(bool enabled = true)
        {
            Options.UseTls = enabled;
            return this;
        }
    }
}
