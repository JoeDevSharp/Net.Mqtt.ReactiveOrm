using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus.Interfaces;
using Mqtt.net.ORM.Interfaces;

namespace Mqtt.net.ORM
{
    public class TopicSet<T> : ITopicSet<T>, IObservable<T>
    {
        public IMqttBus MqttBus => _mqttBus;

        public TopicAttribute Attribute => _attribute;

        public string Template => _attribute.Template;

        public bool AllowWildcards => _attribute.AllowWildcards;

        private readonly IMqttBus _mqttBus;
        private readonly TopicAttribute _attribute;

        private IObservable<T> _observable => _mqttBus.GetObservable<T>(_attribute);

        public TopicSet(IMqttBus mqttBus, TopicAttribute attribute)
        {
            _mqttBus = mqttBus ?? throw new ArgumentNullException(nameof(mqttBus));
            _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }

        public IObservable<T> Observable() => _observable;

        /// <summary>
        /// Soporta suscripción directa cuando TopicSet<T> se usa como IObservable<T>
        /// </summary>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return _observable.Subscribe(observer);
        }

        public void Publish(T message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _mqttBus.PublishAsync<T>(message, _attribute).GetAwaiter().GetResult();
        }

        public void Unsubscribe()
        {
            _mqttBus.UnsubscribeAsync<T>(_attribute).GetAwaiter().GetResult();
        }
    }
}
