using MqttReactiveObjectMapper.Attributes;
using MqttReactiveObjectMapper.Bus.Interfaces;
using MqttReactiveObjectMapper.Interfaces;
using System.Reactive.Linq;

namespace MqttReactiveObjectMapper
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

        /// <summary>
        /// Filtra los elementos de una secuencia observable según un predicado.
        /// </summary>
        /// <param name="predicate">Función para probar cada elemento de la secuencia.</param>
        /// <returns>Una secuencia observable que contiene los elementos que cumplen la condición.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> es null.</exception>
        public IObservable<T> Where(Func<T, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            return _observable.Where(predicate);
        }

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

        public IObservable<T> Observable()
        {
            return _observable;
        }
    }
}
