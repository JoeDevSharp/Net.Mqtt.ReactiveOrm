using Net.Mqtt.ReactiveOrm;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Models;

public class DHT230222_Modules
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MqttContext : MqttOrmContext
{
    /// <summary>
    /// Flujo de eventos con objeto complejo, sin retención.
    /// </summary>
    public TopicSet<DHT230222_Modules> DHT230222_Modules => Set<DHT230222_Modules>();

    /// <summary>
    /// Otro tópico con la misma clase en diferente ruta, QoS intermedio.
    /// </summary>
    public TopicSet<DHT230222_Modules> Z_XTR_Modules => Set<DHT230222_Modules>();

    /// <summary>
    /// Tópico que publica datos primitivos.
    /// </summary>
    public TopicSet<double> TemperatureRaw => Set<double>();

    /// <summary>
    /// Tópico que permite comodines para suscripción dinámica.
    /// </summary>
    public TopicSet<DHT230222_Modules> AllSensors => Set<DHT230222_Modules>();

    /// <summary>
    /// Tópico que permite comodines para suscripción dinámica.
    /// </summary>
    public TopicSet<DHT230222_Modules> AllSensorMessages => Set<DHT230222_Modules>();

    /// <summary>
    /// Constructor por defecto para usar con broker local.
    /// </summary>
    public MqttContext(IMqttBus bus, ITopicModel model) : base(bus, model) { }

    public static TopicModel CreateModel() => new TopicModelBuilder()
        .Add<DHT230222_Modules>(nameof(DHT230222_Modules), "factory_64/sensors/@/events", QoSLevel.ExactlyOnce)
        .Add<DHT230222_Modules>(nameof(Z_XTR_Modules), "factory_64/module/@/status", QoSLevel.AtLeastOnce)
        .Add<double>(nameof(TemperatureRaw), "factory_64/sensors/temperature/value")
        .Add<DHT230222_Modules>(nameof(AllSensors), "factory_64/+/+/status")
        .Add<DHT230222_Modules>(nameof(AllSensorMessages), "factory_64/sensors/#")
        .Build();
}
