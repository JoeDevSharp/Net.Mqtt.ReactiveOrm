using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus.Interfaces;
using MQTTnet;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;

namespace Mqtt.net.ORM.Bus
{
    /// <summary>
    /// Default implementation of IMqttBus using MQTTnet.
    /// Handles reactive subscription and publishing of strongly-typed messages over MQTT.
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
        /// Constructs an instance of MqttBus.
        /// </summary>
        public MqttBus(IMqttClient client, MqttClientOptions options, MqttSerializer serializer)
        {
            _client = client;
            _options = options;
            _serializer = serializer;

            // Hook into the message received event to dispatch messages to registered handlers
            _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        }

        /// <summary>
        /// Connects to the MQTT broker if not already connected.
        /// </summary>
        public async Task ConnectAsync()
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(_options);
            }
        }

        /// <summary>
        /// Returns an observable stream for a given message type and topic attribute.
        /// Automatically subscribes to the topic if needed.
        /// </summary>
        public IObservable<T> GetObservable<T>(TopicAttribute attribute)
        {
            // Check if the observable already exists
            if (!_subjects.TryGetValue(typeof(T), out var subjectObj))
            {
                var subject = new Subject<T>();
                _subjects[typeof(T)] = subject;
                _topicAttributes[typeof(T)] = attribute;

                // Ensure subscription is active before returning the observable
                EnsureSubscribedAsync<T>().GetAwaiter().GetResult();

                return subject.AsObservable();
            }

            return ((ISubject<T>)subjectObj).AsObservable();
        }

        /// <summary>
        /// Ensures that a given message type is subscribed to only once.
        /// </summary>
        private Task EnsureSubscribedAsync<T>()
        {
            var type = typeof(T);

            return _subscriptionTasks.GetOrAdd(type, async _ =>
            {
                // Validate topic attribute registration
                if (!_topicAttributes.TryGetValue(type, out var attribute))
                    throw new InvalidOperationException($"Tipo {type.Name} no registrado. Usa Register<T>(TopicAttribute)");

                var topic = attribute.Resolve(Activator.CreateInstance(type));

                // Register message handler
                _handlers[topic] = async raw =>
                {
                    var message = _serializer.Deserialize<T>(raw);
                    if (_subjects.TryGetValue(type, out var subj))
                    {
                        ((ISubject<T>)subj).OnNext(message);
                    }
                    await Task.CompletedTask;
                };

                // Connect to broker and subscribe to the topic
                await ConnectAsync();
                await _client.SubscribeAsync(topic, attribute.QoS);
            });
        }

        /// <summary>
        /// Publishes a strongly‑typed message to its resolved topic.
        /// </summary>
        public async Task PublishAsync<T>(object message, TopicAttribute attribute)
        {
            // Resolve topic from attribute
            var topic = attribute.Resolve(Activator.CreateInstance<T>());
            var payload = _serializer.Serialize(message);

            // Build MQTT message
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(attribute.QoS)
                .Build();

            // Ensure connection and publish
            await ConnectAsync();
            await _client.PublishAsync(msg);
            Console.WriteLine($"Published : {typeof(T).Name} to topic {topic}");
        }

        /// <summary>
        /// Subscribes a handler for a message type, and pushes messages into observable stream if it exists.
        /// </summary>
        public async Task SubscribeAsync<T>(Func<T, Task> handler, TopicAttribute attribute)
        {
            var topic = attribute.Resolve(Activator.CreateInstance<T>());

            // Register the handler
            _handlers[topic] = async raw =>
            {
                // Deserialize message
                var message = _serializer.Deserialize<T>(raw);

                // Push to observable if present
                if (_subjects.TryGetValue(typeof(T), out var subjObj))
                {
                    var subject = (ISubject<T>)subjObj;
                    subject.OnNext(message);
                }

                // Invoke original handler
                await handler(message);
            };

            // Connect and subscribe to topic
            await ConnectAsync();
            await _client.SubscribeAsync(topic, attribute.QoS);
        }

        /// <summary>
        /// Unsubscribes the handler for a given message type.
        /// </summary>
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
                throw new InvalidOperationException($"No handler for '{topic}' to unsubscribe.");
            }
        }

        #region Private helpers

        /// <summary>
        /// Central message-dispatch entry point. Triggers handler based on topic.
        /// </summary>
        private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            // Try exact match first
            if (_handlers.TryGetValue(topic, out var handler))
            {
                await handler(payload);
                return;
            }

            // Fallback to wildcard pattern matching
            foreach (var kv in _handlers)
            {
                if (MatchTopic(kv.Key, topic))
                {
                    await kv.Value(payload);
                }
            }
        }

        /// <summary>
        /// Deserializes payload and invokes the handler.
        /// </summary>
        private async Task DispatchAsync<T>(string raw, Func<T, Task> handler)
        {
            var obj = _serializer.Deserialize<T>(raw);
            await handler(obj);
        }

        /// <summary>
        /// Simple MQTT wildcard matcher (‘+’ and ‘#’).
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
