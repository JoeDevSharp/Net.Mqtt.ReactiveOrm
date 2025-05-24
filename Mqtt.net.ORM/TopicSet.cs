using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus.Interfaces;

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
        public void PublishAsync(T message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _mqttBus.PublishAsync<T>(message, _attribute).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Unsubscribes from the topic for messages of type T.
        /// </summary>
        public void Unsubscribe()
        {
            _mqttBus.UnsubscribeAsync<T>(_attribute).GetAwaiter().GetResult();
        }
    }
}
