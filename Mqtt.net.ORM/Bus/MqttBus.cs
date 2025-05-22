using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus.Interfaces;
using MQTTnet;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Mqtt.net.ORM.Bus
{
    /// <summary>
    /// Default implementation of IMqttBus using MQTTnet.
    /// </summary>
    public class MqttBus : IMqttBus
    {
        private readonly IMqttClient _client;
        private readonly MqttClientOptions _options;
        private readonly MqttSerializer _serializer;
        private readonly ConcurrentDictionary<string, Func<string, Task>> _handlers = new();

        /// <summary>
        /// Constructs an instance of MqttBus.
        /// </summary>
        public MqttBus(IMqttClient client, MqttClientOptions options, MqttSerializer serializer)
        {
            _client = client;
            _options = options;
            _serializer = serializer;

            // Hook into the message received event
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
        /// Publishes a strongly‑typed message to its resolved topic.
        /// </summary>
        public async Task PublishAsync<T>(T message)
        {
            var attr = typeof(T).GetCustomAttribute<MqttTopicAttribute>()
                ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' lacks [MqttTopic].");

            var topic = attr.Resolve(message);
            var payload = _serializer.Serialize(message);

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(attr.QoS)
                .Build();

            await ConnectAsync();
            await _client.PublishAsync(msg);
        }

        /// <summary>
        /// Subscribes a handler for a message type, with optional topic parameters.
        /// </summary>
        /// <param name="handler">Async handler to invoke on message reception.</param>
        /// <param name="parameters">Dictionary of template values, if any.</param>
        /// <param name="overwrite">Whether to overwrite an existing handler.</param>
        public async Task SubscribeAsync<T>(Func<T, Task> handler)
        {
            var attr = typeof(T).GetCustomAttribute<MqttTopicAttribute>()
                ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' lacks [MqttTopic].");

            // Resolve template placeholders
            var topic = attr.Resolve(Activator.CreateInstance<T>());

            // Register or replace handler
            _handlers[topic] = raw => DispatchAsync(raw, handler);

            // Connect and subscribe
            await ConnectAsync();
            await _client.SubscribeAsync(topic, attr.QoS);
        }

        /// <summary>
        /// Unsubscribes the handler for a given message type.
        /// </summary>
        public async Task UnsubscribeAsync<T>()
        {
            var attr = typeof(T).GetCustomAttribute<MqttTopicAttribute>()
                ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' lacks [MqttTopic].");

            var topic = attr.Resolve(Activator.CreateInstance<T>());

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
        /// Central message-dispatch entry point.
        /// </summary>
        private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            // Exact match
            if (_handlers.TryGetValue(topic, out var handler))
            {
                await handler(payload);
                return;
            }

            // Wildcard match fallback
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
