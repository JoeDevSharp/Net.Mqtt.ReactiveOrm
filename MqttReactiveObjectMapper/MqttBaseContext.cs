using MqttReactiveObjectMapper.Attributes;
using MqttReactiveObjectMapper.Bus;
using MqttReactiveObjectMapper.Bus.Interfaces;
using MqttReactiveObjectMapper.Models;
using MQTTnet;
using System.Reflection;

namespace MqttReactiveObjectMapper
{
    /// <summary>
    /// Clase base para el contexto MQTT que configura y administra la conexión MQTT, 
    /// el bus de mensajes y la inicialización automática de TopicSets definidos en las propiedades públicas.
    /// </summary>
    public class MqttBaseContext
    {
        /// <summary>
        /// Instancia interna de opciones para almacenar los valores de configuración del servidor MQTT.
        /// </summary>
        private MqttServerOptions Options { get; } = new();

        /// <summary>
        /// Bus de mensajes MQTT utilizado para enviar y recibir datos entre tópicos.
        /// </summary>
        public MqttBus MqttBus;

        /// <summary>
        /// Constructor principal del contexto MQTT.
        /// Inicializa la conexión MQTT con el host y puerto especificados, crea el cliente, 
        /// configura las opciones y prepara los TopicSets definidos en la clase derivada.
        /// </summary>
        /// <param name="host">Dirección del servidor MQTT (por defecto "localhost").</param>
        /// <param name="port">Puerto del servidor MQTT (por defecto 1883).</param>
        public MqttBaseContext(string host = "localhost", int port = 1883, string? username = null, string? password = null)
        {
            Options.Server = host;
            Options.Port = port;

            var clientFactory = new MqttClientFactory();
            var mqttClient = clientFactory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(Options.Server, Options.Port)
                .WithClientId(Options.ClientId)
                .WithCredentials(username, password)
                .Build();

            var serializer = new MqttSerializer();

            MqttBus = new MqttBus(mqttClient, options, serializer);

            InitializeTopicSets();
        }

        /// <summary>
        /// Inicializa automáticamente las propiedades públicas que heredan de TopicSet<T> 
        /// y tienen el atributo TopicAttribute, inyectando el bus MQTT y los metadatos del tópico.
        /// </summary>
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
