// Importa el Worker que procesará los eventos MQTT.
using Demo;
// Importa el contrato de datos registrado en el metamodelo.
using Demo.Entities;
// Importa el contexto que declara los TopicSet mediante atributos.
using Demo.Mqtt;
// Habilita la API fluent y el registro de servicios.
using Microsoft.Extensions.DependencyInjection;
// Proporciona el Generic Host y su ciclo de vida.
using Microsoft.Extensions.Hosting;

// Crea el Host con logging, configuración y cancelación mediante Ctrl+C.
var builder = Host.CreateApplicationBuilder(args);

// Configura toda la infraestructura MQTT desde un único punto de entrada.
builder.Services.AddMqttReactiveOrm<MqttContext>(mqtt =>
{
    // Configura conexión, identidad técnica, namespace y source CloudEvents mediante una cadena fluent.
    mqtt.ConnectTo("localhost", 1883)
        .IdentifyAs("reactive-orm-demo-worker")
        .ForModule("factory_64")
        .WithCloudEventSource("urn:factory:equipment-worker")
        .UseDevelopmentDefaults()
        .UseUnavailableLastWill();

    // Registra la relación type CloudEvents, dataschema, versión y tipo C#.
    mqtt.UseContracts(contracts => contracts.Add<DHT230222_Modules>(
        eventType: "com.factory.sensor.reading.v1",
        dataSchema: new Uri("urn:schema:factory:sensor-reading:v1"),
        version: new Version(1, 0, 0),
        maximumDataSize: 16 * 1024,
        forbiddenFields: ["password", "secret"]));

    // Registra el JSON Schema local; se puede sustituir por un resolver de archivos o HTTP.
    mqtt.UseSchemas(schemas => schemas.AddInline(
        uri: "urn:schema:factory:sensor-reading:v1",
        jsonSchema:
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "required": ["temperature", "humidity", "timestamp"],
          "properties": {
            "temperature": { "type": "number", "minimum": -273.15 },
            "humidity": { "type": "number", "minimum": 0, "maximum": 100 },
            "timestamp": { "type": "string" }
          },
          "additionalProperties": false
        }
        """,
        version: "1.0.0")
    );
});

// Añade únicamente el Worker aplicativo; contexto y dependencias MQTT ya están registrados.
builder.Services.AddHostedService<SensorWorker>();

// Construye y ejecuta el Host hasta recibir la señal de parada.
await builder.Build().RunAsync();
