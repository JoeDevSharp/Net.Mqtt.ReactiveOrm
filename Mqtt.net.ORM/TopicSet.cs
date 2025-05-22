using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus.Interfaces;

namespace Mqtt.net.ORM
{
    public class TopicSet<T> where T : class
    {
        private readonly IMqttBus _mqttBus;
        private readonly string _template;
        private readonly bool _allowWildcards;

        public TopicSet(IMqttBus mqttBus)
        {
            _mqttBus = mqttBus ?? throw new ArgumentNullException(nameof(mqttBus));

            var topicAttribute = typeof(T).GetCustomAttributes(typeof(MqttTopicAttribute), false)
                .FirstOrDefault() as MqttTopicAttribute;

            if (topicAttribute == null)
                throw new InvalidOperationException($"Type {typeof(T).Name} is not decorated with MqttTopicAttribute.");

            _template = topicAttribute.Template;
            _allowWildcards = topicAttribute.AllowWildcards;
        }

        /// <summary>
        /// Publishes a message of type T to the resolved topic.
        /// </summary>
        public async Task PublishAsync(T message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            await _mqttBus.PublishAsync(message);
        }

        /// <summary>
        /// Subscribes to a topic for messages of type T and invokes the callback when messages arrive.
        /// </summary>
        public async Task SubscribeAsync(Func<T, Task> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            await _mqttBus.SubscribeAsync(callback);
        }

        /// <summary>
        /// Unsubscribes from the topic for messages of type T.
        /// </summary>
        public async Task UnsubscribeAsync()
        {
            await _mqttBus.UnsubscribeAsync<T>();
        }
    }
}
