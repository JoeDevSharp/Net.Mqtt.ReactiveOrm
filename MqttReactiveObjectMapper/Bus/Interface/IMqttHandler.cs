namespace MqttReactiveObjectMapper.Bus.Interfaces
{
    /// <summary>
    /// Define un manejador para mensajes MQTT entrantes del tipo <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Tipo del mensaje que este manejador procesará.</typeparam>
    public interface IMqttHandler<T>
    {
        /// <summary>
        /// Procesa un mensaje MQTT recibido.
        /// </summary>
        /// <param name="message">Objeto del mensaje deserializado desde el payload MQTT.</param>
        Task HandleAsync(T message);
    }
}
