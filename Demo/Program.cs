// Imports the Event Entities exchanged through CloudEvents.
using Demo.Entities;
// Imports the context that declares the application's typed TopicSet instances.
using Demo.Mqtt;
// Imports the services that contain the BS1 and BS2 business rules.
using Demo.Services;
// Imports the BackgroundService implementations that orchestrate the use cases.
using Demo.Workers;
// Enables dependency and Hosted Service registration.
using Microsoft.Extensions.DependencyInjection;
// Provides Generic Host, configuration, logging, and graceful shutdown.
using Microsoft.Extensions.Hosting;

// Creates the demo Host. The Host cancellation token is propagated to every Worker.
var builder = Host.CreateApplicationBuilder(args);

// Registers the complete MQTT infrastructure and typed context through one fluent entry point.
// The library creates a singleton IMqttBus, so every Worker shares the same connection.
builder.Services.AddMqttReactiveOrm<MqttContext>(mqtt =>
{
    // Configures the transport and the process's technical identity.
    mqtt.ConnectTo("localhost", 1883)
        // Uses a stable ClientId so the broker can identify this instance.
        .IdentifyAs("business-workers-demo")
        // Prefixes all relative [MqttTopic] publication and subscription addresses.
        .WithBaseTopic("factory_64")
        // Places every topic under factory_64/moduls/factory/services/business-workers.
        .ForModule("factory")
        .ForService("business-workers")
        // Defines the CloudEvents source attribute for every published event.
        .WithCloudEventSource("urn:factory:business-workers")
        // Enables MQTT 5, a clean session, and fast reconnection for local development.
        .UseDevelopmentDefaults()
        // Declares an UNAVAILABLE Last Will CloudEvent for an ungraceful connection loss.
        .UseUnavailableLastWill();

    // Registers each attributed Event Entity with its governed CloudEvents identity.
    // The registry contains no topics; they are declared once in MqttContext.
    mqtt.UseEventEntities(entities =>
    {
        // BS1 input: temperature and humidity telemetry produced by sensor1.
        entities.Add<Sensor1Telemetry>();

        // BS2 input: one binary fragment from the sensor2 video stream.
        // byte[] is serialized as Base64 inside the CloudEvent JSON data value.
        entities.Add<Sensor2VideoChunk>();

        // BS1 output: an operational decision produced by Worker BS1 and BusinessServiceBs1.
        entities.Add<Bs1OperationalAssessment>();

        // BS2 output: the result published after the video stream has been assembled.
        entities.Add<Bs2VideoResult>();
    });

    // Registers the JSON Schema associated with each dataschema above.
    // The library validates data before publication and before delivery to a Worker.
    mqtt.UseSchemas(schemas =>
    {
        // Input contracts received from devices remain inline to demonstrate
        // a self-contained configuration that is easy to run.
        schemas.AddInline("urn:schema:factory:bs1-environment-input:v1",
            """
            { "type":"object", "required":["sensorId","temperature","humidity","observedAt"],
              "properties":{ "sensorId":{"type":"string"}, "temperature":{"type":"number"},
                "humidity":{"type":"number","minimum":0,"maximum":100}, "observedAt":{"type":"string"} },
              "additionalProperties":false }
            """,
            version: "1.0.0");

        // The binary payload is represented as a Base64 string in JSON.
        // Capacity and maximumDataSize prevent an entire stream from using one message.
        schemas.AddInline("urn:schema:factory:bs2-video-chunk:v1",
            """
            { "type":"object", "required":["cameraId","streamId","sequence","isFinal","mediaType","payload","capturedAt"],
              "properties":{ "cameraId":{"type":"string"}, "streamId":{"type":"string"},
                "sequence":{"type":"integer","minimum":0}, "isFinal":{"type":"boolean"},
                "mediaType":{"type":"string"}, "payload":{"type":"string"}, "capturedAt":{"type":"string"} },
              "additionalProperties":false }
            """,
            version: "1.0.0");

        // Contracts produced by business processes are stored in separate files.
        // AppContext.BaseDirectory points to the output; Demo.csproj copies Schemas there.
        schemas.Add("urn:schema:factory:bs1-operational-assessment:v1",
            Path.Combine(
                AppContext.BaseDirectory,
                "Schemas",
                "bs1-operational-assessment-v1.schema.json"));

        schemas.Add("urn:schema:factory:bs2-video-result:v1",
            Path.Combine(
                AppContext.BaseDirectory,
                "Schemas",
                "bs2-video-result-v1.schema.json"));
    });
});

// Registers BS1 as a singleton because it keeps no per-message state.
builder.Services.AddSingleton<IBusinessServiceBs1, BusinessServiceBs1>();
// Registers BS2 as a singleton because it temporarily retains chunks for each stream.
builder.Services.AddSingleton<IBusinessServiceBs2, BusinessServiceBs2>();

// Observes MQTT state changes and certificate-expiration warnings.
builder.Services.AddHostedService<MqttLifecycleWorker>();
// Consumes sensor1, runs BS1, publishes Bs1OperationalAssessment, and then acknowledges.
builder.Services.AddHostedService<BusinessWorkerBs1>();
// Consumes sensor2 with backpressure, runs BS2, and publishes the final result.
builder.Services.AddHostedService<BusinessWorkerBs2>();
// Simulates external sources after IMqttBus reaches the Ready state.
builder.Services.AddHostedService<BusinessInputSimulatorWorker>();

// Builds the container, connects MQTT through the library Hosted Service,
// and keeps the process running until Ctrl+C or a Host shutdown signal.
await builder.Build().RunAsync();
