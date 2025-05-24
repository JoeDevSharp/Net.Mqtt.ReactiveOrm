using Codevia.MqttReactiveObjectMapper;
using Codevia.MqttReactiveObjectMapper.Attributes;
using Codevia.MqttReactiveObjectMapper.Enums;

public class DHT230222_Modules
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MqttContext : MqttBaseContext
{
    /// <summary>
    /// Tópico con objeto complejo, QoS alto y retención activada.
    /// </summary>
    [Topic("factory_64/sensors/@/status", QoSLevel.ExactlyOnce, retain: true)]
    public TopicSet<DHT230222_Modules> DHT230222_Modules { get; set; }

    /// <summary>
    /// Otro tópico con la misma clase en diferente ruta, QoS intermedio.
    /// </summary>
    [Topic("factory_64/module/@/status", QoSLevel.AtLeastOnce)]
    public TopicSet<DHT230222_Modules> Z_XTR_Modules { get; set; }

    /// <summary>
    /// Tópico que publica datos primitivos.
    /// </summary>
    [Topic("factory_64/sensors/temperature/value")]
    public TopicSet<double> TemperatureRaw { get; set; }

    /// <summary>
    /// Tópico que permite comodines para suscripción dinámica.
    /// </summary>
    [Topic("factory_64/+/+/status")]
    public TopicSet<DHT230222_Modules> AllSensors { get; set; }

    /// <summary>
    /// Tópico que permite comodines para suscripción dinámica.
    /// </summary>
    [Topic("factory_64/sensors/#")]
    public TopicSet<DHT230222_Modules> AllSensorMessages { get; set; }

    /// <summary>
    /// Constructor por defecto para usar con broker local.
    /// </summary>
    public MqttContext() : base("localhost", 1883) { }
}
