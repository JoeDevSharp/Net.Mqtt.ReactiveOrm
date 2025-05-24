using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus.Interfaces;
using System.Threading.Tasks;

namespace Mqtt.net.ORM
{
    public class TopicSet<T>
    {
        /// <summary>
        /// The MQTT topic template (e.g., sensors/{deviceId}/temperature)
        /// </summary>
        public string Template => _attribute.Template;
        /// <summary>
        /// Whether this topic supports MQTT wildcards (+/#) for subscriptions.
        /// </summary>
        public bool AllowWildcards => _attribute.AllowWildcards;
     
        private readonly IMqttBus _mqttBus;
        private readonly TopicAttribute _attribute;


        //private readonly string _template;
        //private readonly bool _allowWildcards;

        public TopicSet(IMqttBus mqttBus, TopicAttribute attribute)
        {
            _mqttBus = mqttBus ?? throw new ArgumentNullException(nameof(mqttBus));

            _attribute = attribute;

            //_template = topicAttribute.Template;
            //_allowWildcards = topicAttribute.AllowWildcards;
        }

        public IObservable<T> Observable()
        {
            return _mqttBus.GetObservable<T>(_attribute);
        }



        /// <summary>
        /// Publishes a message of type T to the resolved topic.
        /// </summary>
        public async Task PublishAsync(T message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            await _mqttBus.PublishAsync<T>(message, _attribute);
        }

        /// <summary>
        /// Subscribes to a topic for messages of type T and invokes the callback when messages arrive.
        /// </summary>
        public async Task SubscribeAsync(Func<T, Task> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            await _mqttBus.SubscribeAsync(callback, _attribute);
        }

        /// <summary>
        /// Unsubscribes from the topic for messages of type T.
        /// </summary>
        public async Task UnsubscribeAsync()
        {
            await _mqttBus.UnsubscribeAsync<T>(_attribute);
        }
    }
}
