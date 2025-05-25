using System.Reactive.Linq;

namespace Codevia.MqttReactiveObjectMapper
{
    public partial class TopicSet<T>
    {

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
    }
}
