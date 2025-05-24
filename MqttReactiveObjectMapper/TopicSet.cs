using Codevia.MqttReactiveObjectMapper.Attributes;
using Codevia.MqttReactiveObjectMapper.Bus.Interfaces;
using Codevia.MqttReactiveObjectMapper.Enums;
using Codevia.MqttReactiveObjectMapper.Interfaces;
using MQTTnet.Protocol;
using System.Reactive.Linq;

namespace Codevia.MqttReactiveObjectMapper
{
    /// <summary>
    /// Representa un conjunto reactivo de tópicos MQTT fuertemente tipados.
    /// Permite publicar y suscribirse a mensajes de tipo <typeparamref name="T"/> sobre un tópico específico.
    /// </summary>
    public class TopicSet<T> : ITopicSet<T>, IObservable<T>
    {
        /// <summary>
        /// Instancia del bus MQTT utilizado por este conjunto de tópicos.
        /// </summary>
        public IMqttBus MqttBus => _mqttBus;

        /// <summary>
        /// Atributo del tópico que define la plantilla, QoS, retención y comodines.
        /// </summary>
        public TopicAttribute Attribute => _attribute;

        /// <summary>
        /// Plantilla del tópico MQTT.
        /// </summary>
        public string Template => _attribute.Template;

        /// <summary>
        /// Indica si se permiten comodines en la suscripción.
        /// </summary>
        public bool AllowWildcards => _attribute.AllowWildcards;

        private readonly IMqttBus _mqttBus;
        private readonly TopicAttribute _attribute;

        private IObservable<T> _observable => _mqttBus.GetObservable<T>(_attribute);

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="TopicSet{T}"/>.
        /// </summary>
        /// <param name="mqttBus">Instancia del bus MQTT.</param>
        /// <param name="attribute">Atributo del tópico asociado.</param>
        /// <exception cref="ArgumentNullException">Si <paramref name="mqttBus"/> o <paramref name="attribute"/> es null.</exception>
        public TopicSet(IMqttBus mqttBus, TopicAttribute attribute)
        {
            _mqttBus = mqttBus ?? throw new ArgumentNullException(nameof(mqttBus));
            _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }

        /// <summary>
        /// Filtra los elementos de una secuencia observable según un predicado.
        /// </summary>
        /// <param name="predicate">Función que determina si un elemento debe incluirse.</param>
        /// <returns>Una secuencia observable con los elementos que cumplen la condición.</returns>
        /// <exception cref="ArgumentNullException">Si <paramref name="predicate"/> es null.</exception>
        public IObservable<T> Where(Func<T, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            return _observable.Where(predicate);
        }

        /// <summary>
        /// Permite suscribirse a los mensajes publicados en el tópico.
        /// </summary>
        /// <param name="observer">Observador que recibirá los mensajes.</param>
        /// <returns>Un <see cref="IDisposable"/> que permite cancelar la suscripción.</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return _observable.Subscribe(observer);
        }

        /// <summary>
        /// Publica un mensaje usando los valores de QoS y retención definidos por defecto en el atributo del tópico.
        /// </summary>
        /// <param name="message">Mensaje a publicar.</param>
        /// <exception cref="ArgumentNullException">Si <paramref name="message"/> es null.</exception>
        public void Publish(T message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _mqttBus.PublishAsync<T>(message, _attribute).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Publica un mensaje sobreescribiendo los valores de QoS y retención para esta publicación específica.
        /// </summary>
        /// <param name="message">Mensaje a publicar.</param>
        /// <param name="qos">Nivel de QoS a utilizar para esta publicación.</param>
        /// <param name="retain">Indica si el mensaje debe ser retenido.</param>
        /// <exception cref="ArgumentNullException">Si <paramref name="message"/> es null.</exception>
        public void Publish(T message, QoSLevel qos, bool retain)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var mqttQoS = (MqttQualityOfServiceLevel)qos;
            _mqttBus.PublishAsync<T>(message, _attribute, mqttQoS, retain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Cancela la suscripción al tópico.
        /// </summary>
        public void Unsubscribe()
        {
            _mqttBus.UnsubscribeAsync<T>(_attribute).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Obtiene la secuencia observable de mensajes del tópico.
        /// </summary>
        /// <returns>Una instancia de <see cref="IObservable{T}"/>.</returns>
        public IObservable<T> Observable()
        {
            return _observable;
        }
    }
}
