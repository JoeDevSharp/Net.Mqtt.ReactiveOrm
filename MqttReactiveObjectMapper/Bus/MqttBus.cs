using Codevia.MqttReactiveObjectMapper.Attributes;
using Codevia.MqttReactiveObjectMapper.Bus.Interfaces;
using MQTTnet;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;

namespace Codevia.MqttReactiveObjectMapper.Bus
{
    /// <summary>
    /// Implementación por defecto de <see cref="IMqttBus"/> utilizando MQTTnet.
    /// Gestiona suscripciones reactivas y la publicación de mensajes fuertemente tipados sobre MQTT.
    /// </summary>
    public class MqttBus : IMqttBus
    {
        private readonly IMqttClient _client;
        private readonly MqttClientOptions _options;
        private readonly MqttSerializer _serializer;
        private readonly ConcurrentDictionary<string, Func<string, Task>> _handlers = new();
        private readonly Dictionary<Type, object> _subjects = new();
        private readonly HashSet<Type> _subscribedTypes = new();
        private readonly Dictionary<Type, TopicAttribute> _topicAttributes = new();
        private readonly ConcurrentDictionary<Type, Task> _subscriptionTasks = new();

        /// <summary>
        /// Crea una nueva instancia de <see cref="MqttBus"/>.
        /// </summary>
        /// <param name="client">Instancia del cliente MQTT.</param>
        /// <param name="options">Opciones de conexión para el cliente MQTT.</param>
        /// <param name="serializer">Serializador para transformar mensajes.</param>
        public MqttBus(IMqttClient client, MqttClientOptions options, MqttSerializer serializer)
        {
            _client = client;
            _options = options;
            _serializer = serializer;

            // Registro del evento para manejar mensajes recibidos
            _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        }

        /// <inheritdoc />
        public async Task ConnectAsync()
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(_options);
            }
        }

        /// <inheritdoc />
        public IObservable<T> GetObservable<T>(TopicAttribute attribute)
        {
            if (!_subjects.TryGetValue(typeof(T), out var subjectObj))
            {
                var subject = new Subject<T>();
                _subjects[typeof(T)] = subject;
                _topicAttributes[typeof(T)] = attribute;

                // Garantiza que se haya realizado la suscripción
                EnsureSubscribedAsync<T>().GetAwaiter().GetResult();

                return subject.AsObservable();
            }

            return ((ISubject<T>)subjectObj).AsObservable();
        }

        /// <summary>
        /// Garantiza que el tipo de mensaje <typeparamref name="T"/> esté suscrito solo una vez.
        /// </summary>
        private Task EnsureSubscribedAsync<T>()
        {
            var type = typeof(T);

            return _subscriptionTasks.GetOrAdd(type, async _ =>
            {
                if (!_topicAttributes.TryGetValue(type, out var attribute))
                    throw new InvalidOperationException($"El tipo {type.Name} no está registrado. Usa Register<T>(TopicAttribute).");

                var topic = attribute.Resolve(Activator.CreateInstance(type));

                _handlers[topic] = async raw =>
                {
                    var message = _serializer.Deserialize<T>(raw);
                    if (_subjects.TryGetValue(type, out var subj))
                    {
                        ((ISubject<T>)subj).OnNext(message);
                    }

                    await Task.CompletedTask;
                };

                await ConnectAsync();
                await _client.SubscribeAsync(topic, attribute.QoS);
            });
        }

        /// <inheritdoc />
        public async Task PublishAsync<T>(object message, TopicAttribute attribute)
        {
            var topic = attribute.Resolve(Activator.CreateInstance<T>());
            var payload = _serializer.Serialize(message);

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(attribute.QoS)
                .Build();

            await ConnectAsync();
            await _client.PublishAsync(msg);

            Console.WriteLine($"Mensaje publicado: {typeof(T).Name} en el topic {topic}");
        }

        /// <inheritdoc />
        public async Task UnsubscribeAsync<T>(TopicAttribute attribute)
        {
            var topic = attribute.Resolve(Activator.CreateInstance<T>());

            if (_handlers.TryRemove(topic, out _))
            {
                if (_client.IsConnected)
                {
                    await _client.UnsubscribeAsync(topic);
                }
            }
            else
            {
                throw new InvalidOperationException($"No hay handler registrado para el topic '{topic}' que se pueda cancelar.");
            }
        }

        #region Métodos privados

        /// <summary>
        /// Método central para el despacho de mensajes MQTT entrantes.
        /// Se invoca automáticamente cuando se recibe un mensaje desde el broker.
        /// </summary>
        private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            // Coincidencia exacta del topic
            if (_handlers.TryGetValue(topic, out var handler))
            {
                await handler(payload);
                return;
            }

            // Coincidencia con patrón wildcard
            foreach (var kv in _handlers)
            {
                if (MatchTopic(kv.Key, topic))
                {
                    await kv.Value(payload);
                }
            }
        }

        /// <summary>
        /// Ejecuta la deserialización del mensaje y llama al manejador.
        /// </summary>
        private async Task DispatchAsync<T>(string raw, Func<T, Task> handler)
        {
            var obj = _serializer.Deserialize<T>(raw);
            await handler(obj);
        }

        /// <summary>
        /// Realiza el emparejamiento de topics MQTT con comodines (‘+’, ‘#’).
        /// </summary>
        private static bool MatchTopic(string pattern, string topic)
        {
            var regex = "^" + Regex.Escape(pattern)
                .Replace("\\+", "[^/]+")
                .Replace("\\#", ".*") + "$";
            return Regex.IsMatch(topic, regex);
        }

        #endregion
    }
}
