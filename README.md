# Net.Mqtt.Infrastructure

PRESENTATION

Net.Mqtt.Infrastructure is the common MQTT SDK for .NET Worker Services. It preserves a deliberately small idea—`MQTT topic ↔ TopicSet<TData> ↔ typed stream`—and centralizes production messaging concerns: connection, security, CloudEvents, Event Entities, validation, resilience, and controlled consumption.

The goal of the public API is for the business code to work with `TData` and `MqttMessageContext<TData>`, not MQTTnet clients, bytes, headers, or reconnection logic.

Description

The library relies on MQTTnet, but does not expose it as a dependency of the application context. A single injectable `IMqttBus` shares the connection within the Worker; `MqttOrmContext` declares the topic model; and each `TopicSet<TData>` publishes and consumes CloudEvents 1.0 validated through the Event Entity associated with its C# type.

```text
Topic MQTT
  → transporte seguro y resiliente
  → CloudEvent 1.0
  → Event Entity and JSON Schema
  → TopicSet<TData>
  → IAsyncEnumerable<MqttMessageContext<TData>>
```

The usual configuration uses a single fluent entry point. The library internally registers MQTTnet, CloudEvents, Event Entities, schemas, topics, validators, and lifecycle services.

PURPOSE

Its purpose is to offer all Workers the same technical semantics and avoid different implementations for cross-cutting problems. A producer cannot publish a naked business payload; a consumer does not receive data before validating their CloudEvent and schema; and a delivery is not confirmed until the application runs `AcknowledgeAsync`.

The library is designed for internal MQTT brokers, controlled bridges and contracts generated from a metamodel. Workers remain decoupled from Kafka, other services' databases, and internal transportation details.

CONTENTS

Slide Show - Slide Show
Description
- [Purpose](#purpose)
- [What it solves](#what-it-resolves)
- [Functionalities](#functionalities)
- [Quick Start](#quick-start)
- [Examples by functionality](#examples-by-functionality)
- [Post and Consumption Flow](#post-and-consumption-flow)
- [Presets](#presets)
- [API fluent reference](#api-fluent-reference)
- [Generated Event Entity packages](#generated-event-entity-packages)
- [Dynamic topics](#dynamic-topics)
- [CloudEvents](#cloudevents)
- [Event Entities and schemas](#event-entities-and-schemas)
- [TLS and mTLS](#tls-y-mtls)
- [Lifecycle, sessions and reconnection](#life-cycle-sessions-and-reconnection)
- [Errors and acknowledgements](#errors-and-acknowledgements)
- [Tests](#tests)
- [Current limits](#current-limits)

What does it solve?

The library centralizes the technical decisions that would otherwise end up repeated in each Worker:

| Area | Responsibility |
|---|---|
| Transport | Shared and injectable MQTTnet connection |
| Lifecycle | Coordinated startup and shutdown with Generic Host |
| Resilience | Persistent sessions, reconnection and resubscription |
| Security | Strict TLS/mTLS and Certificate Rotation |
| Envelope | CloudEvents 1.0 structured JSON required |
| Event Entities | Relationship between `type`, `dataschema`, version, and C# type |
| Validation | Limits, forbidden fields and JSON Schema profile |
| Topics | Separation between post and subscription filter |
| Consumption | Cancellation, backpressure and explicit acknowledgement |
| Testing | In-memory transport without real broker |

It is not a broker or a replacement for MQTTnet. It is an application layer that imposes a common protocol on top of MQTTnet.

Features

| Functionality | Result |
|---|---|
| Injectable transport | `IMqttBus` isolates MQTTnet and allows it to be replaced by a bus in memory |
| Asynchronous API | Publish, read, connect, disconnect and acknowledge accept `CancellationToken` |
| MQTT 5 and 3.1.1 | MQTT 5 is the preferred mode; MQTT 3.1.1 retains the structured CloudEvents payload |
| Lifecycle | State Machine, Persistent Sessions, Jitter Reconnect, Resubscribe and LWT |
| TLS and mTLS | Strict server trust, expected name, revocation, client identity and rotation |
| CloudEvents typed | Envelope 1.0 required; `TopicSet<TData>` represents `data` only |
| Event Entities | Unique association between C# type, `type`, `dataschema`, and version |
| Validation | Size, forbidden fields, common JSON profile and JSON Schema subset |
| Topic Model | Explicit Difference Between Post Topic and Subscription Filter |
| Controlled consumption | `IAsyncEnumerable`, bounded channel, backpressure and acknowledgement after processing |
| Rx compatibility | `IObservable<T>` is retained for migrations and scenarios without manual acknowledgement |
| Stability | `InMemoryMqttBus` runs the pipeline without a real broker |

The [Quick Start](#quick-start) and [Examples by functionality](# examples-by-functionality) sections show the implementation of all these capabilities.

Installation

```bash
dotnet add package Net.Mqtt.Infrastructure
```

Requirements:

Net 10
- Broker MQTT 5 or MQTT 3.1.1

Fast Home

### 1. Event Entity

An **Event Entity** is the library standard for representing CloudEvent transport data in the CLR. Its identity and policies are declared exclusively through attributes on the class itself.

```csharp
[EventType("com.factory.sensor.reading.v1")]
[DataSchema("urn:schema:factory:sensor-reading:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(16 * 1024)]
[SchemaCompatibility(ContractCompatibility.SameMajor)]
[CompatibleDataSchema("urn:schema:factory:sensor-reading:v1.1")]
[ForbiddenField("password")]
[ForbiddenField("secret")]
public sealed class SensorReading
{
    public required string SensorId { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

### 2. MQTT Context

The context only declares its topics. It does not build connections or register Event Entities:

```csharp
using Net.Mqtt.Infrastructure;
using Net.Mqtt.Infrastructure.Attributes;
using Net.Mqtt.Infrastructure.Enums;

public sealed class ApplicationMqttContext(
    MqttContextDependencies dependencies)
    : MqttOrmContext(dependencies)
{
    [MqttTopic(
        PublishTopic = "factory/sensors/readings/events",
        SubscribeFilter = "factory/sensors/+/events",
        QoS = MqttQoS.AtLeastOnce,
        Retain = false)]
    public TopicSet<SensorReading> SensorReadings =>
        Set<SensorReading>();
}
```

`PublishTopic` never supports `+`, `#` or `@`. `SubscribeFilter` allows valid MQTT wildcards `+` and `#`.

### 3. Fluent Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMqttReactiveOrm<ApplicationMqttContext>(mqtt =>
{
    mqtt.ConnectTo("localhost", 1883)
        .IdentifyAs("equipment-worker")
        .ForModule("factory")
        .WithCloudEventSource("urn:factory:equipment-worker")
        .UseDevelopmentDefaults()
        .UseUnavailableLastWill();

    mqtt.UseEventEntities(entities =>
        entities.Add<SensorReading>());

    mqtt.UseSchemas(schemas => schemas.AddInline(
        uri: "urn:schema:factory:sensor-reading:v1",
        jsonSchema:
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "required": ["sensorId", "temperature", "humidity", "timestamp"],
          "properties": {
            "sensorId": { "type": "string", "minLength": 1 },
            "temperature": { "type": "number", "minimum": -273.15 },
            "humidity": { "type": "number", "minimum": 0, "maximum": 100 },
            "timestamp": { "type": "string" }
          },
          "additionalProperties": false
        }
        """,
        version: "1.0.0"));
});

builder.Services.AddHostedService<SensorWorker>();
await builder.Build().RunAsync();
```

This single call records:

- `IMqttBus` shared;
- `MqttNetBus`;
- `ITopicModel`;
- `ICloudEventFactory` and `ICloudEventCodec`;
- `IEventEntityRegistry`;
- JSON Schema resolver, cache and validator;
- `MqttContextDependencies`;
- `ApplicationMqttContext`;
- MQTT connection and shutdown service.

### 4. Worker

```csharp
public sealed class SensorWorker(
    ApplicationMqttContext context)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var message in
            context.SensorReadings.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(message.Data, stoppingToken);

            // Confirmar únicamente después del procesamiento correcto.
            await message.AcknowledgeAsync(stoppingToken);
        }
    }
}
```

Release:

```csharp
await context.SensorReadings.PublishAsync(
    new SensorReading
    {
        SensorId = "sensor-42",
        Temperature = 23.7,
        Humidity = 41.2
    },
    stoppingToken);
```

## Examples by functionality

### Injectable transport and shared bus

The context receives all its dependencies by DI. It does not create an MQTTnet client or open its own connection:

```csharp
public sealed class ApplicationMqttContext(MqttContextDependencies dependencies)
    : MqttOrmContext(dependencies)
{
}

public sealed class HealthProbe(IMqttBus bus)
{
    public bool IsReady => bus.IsReady;
}
```

All instances of the Worker's context and services use the same `IMqttBus`. In testing, `UseInMemoryTransport()` replaces the implementation without changing the context or the consumer.

### MQTT 5, MQTT 3.1.1, TCP and WebSocket

MQTT 5 over TCP is the recommended option:

```csharp
mqtt.ConnectTo("mosquitto.internal", 8883)
    .UseMqtt5();
```

To interoperate with an old broker:

```csharp
mqtt.ConnectTo("legacy-broker.internal", 1883)
    .UseMqtt311();
```

For WebSocket, in addition to the transport, the full uri is indicated:

```csharp
mqtt.ConnectTo("mosquitto.internal", 443, MqttTransport.WebSocket);
mqtt.Advanced.WebSocketUri = "wss://mosquitto.internal/mqtt";
```

The CloudEvents structured JSON envelope is mandatory in both versions of the protocol.

### Persistent Session, Reconnection and Last Will

```csharp
mqtt.UsePersistentSession(TimeSpan.FromHours(24))
    .UseExponentialReconnect(
        initial: TimeSpan.FromSeconds(1),
        maximum: TimeSpan.FromMinutes(1))
    .UseUnavailableLastWill("factory/services/equipment-worker/status");
```

MQTT 5 uses `Clean Start` and `Session Expiry Interval`; MQTT 3.1.1 uses `Clean Session = false`. When the broker does not restore the session, the bus automatically restores your subscriptions.

### Status, readiness and session restored

```csharp
public sealed class MqttMonitor(IMqttBus bus)
{
    public void Start()
    {
        bus.StateChanged += (_, change) =>
            Console.WriteLine($"{change.Previous} -> {change.Current}");
    }

    public bool Ready => bus.IsReady;
    public bool SessionWasRestored => bus.WasSessionRestored;
}
```

Readiness must depend on `IsReady`, not just a connected socket.

### TLS/mTLS and Certificate Rotation

From a secretly mounted PFX:

```csharp
mqtt.UseMutualTls(mtls =>
{
    mtls.ClientCertificateProvider =
        new PfxCertificateProvider("/run/secrets/worker.pfx", password);
    mtls.ExpectedServerName = "mosquitto.internal";
    mtls.ExpectedClientIdentity = "equipment-worker";
    mtls.CheckCertificateRevocation = true;
});
```

There are also PEM providers, certificate stores and external secrets:

```csharp
var pem = new PemCertificateProvider("worker.crt", "worker.key");
var store = new StoreCertificateProvider(certificateThumbprint);
var secret = new SecretCertificateProvider(
    token => vault.GetCertificateAsync("equipment-worker", token));

secret.SignalRotation(); // Notifica el cambio y fuerza una reconexión segura.
```

The expiration can be observed without accessing the private certificate:

```csharp
bus.CertificateExpiring += (_, warning) =>
    logger.LogWarning("El certificado {Subject} vence en {Remaining}",
        warning.Subject, warning.Remaining);
```

The server chain, DNS/SAN, validity period and revocation are always validated. Permissive options are not part of the production API.

### CloudEvents and Correlation

```csharp
await context.SensorReadings.PublishAsync(reading,
    new CloudEventPublishOptions
    {
        Context = new CloudEventPublishContext
        {
            Subject = reading.SensorId,
            Extensions = new CloudEventExtensions
            {
                CorrelationId = correlationId,
                CausationId = commandId,
                NegotiationId = negotiationId,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            }
        }
    }, cancellationToken);
```

The SDK generates `specversion`, `id`, `source`, `type`, `datacontenttype`, `dataschema` and `time`. The idempotent identity that the message exposes is the pair `source + id`, never `id` separately.

### Event Entity, version, and JSON Schema

```csharp
mqtt.UseEventEntities(entities => entities.Add<SensorReading>());

mqtt.UseSchemas(schemas => schemas.AddInline(
    "urn:schema:factory:sensor-reading:v1",
    SensorSchemas.ReadingV1,
    "1.0.0"));
```

`EventType`, `DataSchema`, and `EventVersion` are required. `MaximumDataSize`,
`SchemaCompatibility`, `CompatibleDataSchema`, `ForbiddenField`, and
`ContractJsonMapper` are optional. `Add<T>()` throws an explicit exception
identifying the type and any missing or invalid required attributes.

Validation runs before publishing and after receiving, but before exposing `TData`.

### Local, Remote and Cache Schemas

```csharp
mqtt.UseSchemas(schemas => schemas.Use(
    new FileJsonSchemaResolver(new Dictionary<Uri, string>
    {
        [new Uri("urn:schema:factory:sensor-reading:v1")] =
            "/app/contracts/sensor-reading-v1.schema.json"
    })));

mqtt.UseSchemaResolver(
    new HttpJsonSchemaResolver(httpClient),
    cacheCapacity: 128);
```

The HTTP resolver should only point to a trusted contractual repository. The cache is bounded so that an uncontrolled set of URIs does not increase memory indefinitely.

### Explicit Protobuf → JSON projection

```csharp
[EventType("com.factory.sensor.reading.v1")]
[DataSchema("urn:schema:factory:sensor-reading:v1")]
[EventVersion("1.0.0")]
[ContractJsonMapper(typeof(GeneratedReadingMapper))]
public sealed partial class GeneratedReading;

public sealed class GeneratedReadingMapper : IContractJsonMapper
{
    public ReadOnlyMemory<byte> Serialize(object data, Type dataType) =>
        GeneratedReadingJson.Serialize((GeneratedReading)data);

    public object Deserialize(ReadOnlyMemory<byte> json, Type dataType) =>
        GeneratedReadingJson.Deserialize(json);
}

mqtt.UseEventEntities(entities => entities.Add<GeneratedReading>());
```

An arbitrary Protobuf conversion is not inferred: the contractual package governs the JSON representation that is validated and transported.

### Static and dynamic topics

A static topic is declared only once in `[MqttTopic]`; the contractual record provides `type` and `dataschema`:

```csharp
[MqttTopic(
    PublishTopic = "factory/sensors/readings/events",
    SubscribeFilter = "factory/sensors/+/events",
    QoS = MqttQoS.AtLeastOnce)]
public TopicSet<SensorReading> SensorReadings => Set<SensorReading>();
```

For calculated topics, `PublishTopic` is omitted and `ResolverType` is used; there is a complete example in [Dynamic Topics](#dynamic-topics).

### Asynchronous consumption, backpressure and acknowledgement

```csharp
await foreach (var message in context.SensorReadings.ReadAllAsync(
    new SubscriptionOptions { Capacity = 32 }, cancellationToken))
{
    await handler.HandleAsync(message.Data, cancellationToken);
    await message.AcknowledgeAsync(cancellationToken);
}
```

The bounded channel slows down the reading when the consumer is late. If the handler fails, the acknowledgement is not executed and MQTT can re-deliver the message based on its QoS and session.

### Reactive compatibility

```csharp
using var subscription = context.SensorReadings
    .Where(reading => reading.Temperature > 30)
    .Subscribe(reading => logger.LogWarning(
        "Temperatura alta: {Temperature}", reading.Temperature));
```

Rx is maintained for compatibility and simple flows. For workers, `ReadAllAsync` is recommended, because it propagates cancellation, applies backpressure and delivers the necessary context to confirm after the business result.

### Non-Mosquitto Testing

```csharp
services.AddMqttReactiveOrm<TestMqttContext>(mqtt =>
{
    mqtt.UseInMemoryTransport()
        .ForModule("tests")
        .WithCloudEventSource("urn:tests:sensor-worker");
    mqtt.UseEventEntities(RegisterEventEntities);
    mqtt.UseSchemas(RegisterSchemas);
});
```

The same test can solve `TestMqttContext`, publish and read through the real API, without ports, containers or network waits.

## Publishing and Consumption Flow

Publication

```text
TData
  → resolve the Event Entity by C# type
  → comprobar CloudEvent type + dataschema
  → serializar con el perfil JSON determinista
  → validar tamaño y campos prohibidos
  → validar JSON Schema
  → crear CloudEvent 1.0
  → resolver y validar PublishTopic
  → publicar mediante IMqttBus
```

The posting stops before touching the broker if the contract or schema is invalid.

To add technical metadata:

```csharp
await context.SensorReadings.PublishAsync(
    reading,
    new CloudEventPublishOptions
    {
        QoS = QoSLevel.AtLeastOnce,
        Retain = false,
        Context = new CloudEventPublishContext
        {
            Subject = reading.SensorId,
            Extensions = new CloudEventExtensions
            {
                CorrelationId = correlationId,
                CausationId = commandId,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            }
        }
    },
    stoppingToken);
```

If an active `Activity` exists, `traceparent` and `tracestate` are copied automatically when not manually specified.

Consumption

```text
MqttDelivery
  → validar Content Type y envelope CloudEvents
  → resolve the Event Entity by CloudEvent type
  → comprobar dataschema + versión + TData
  → validar límites y JSON Schema sobre data sin deserializarla
  → deserializar TData
  → entregar MqttMessageContext<TData>
  → procesar
  → acknowledge
```

The default capacity of a subscription is 128 messages. Can be adjusted to apply backpressure:

```csharp
await foreach (var message in context.SensorReadings.ReadAllAsync(
    new SubscriptionOptions
    {
        Capacity = 32,
        QoS = QoSLevel.AtLeastOnce
    },
    stoppingToken))
{
    await HandleAsync(message.Data, stoppingToken);
    await message.AcknowledgeAsync(stoppingToken);
}
```

Presets

PROGRESS

```csharp
mqtt.UseDevelopmentDefaults();
```

Activate MQTT 5, clean session without persistence and quick reconnection. This prevents Mosquitto from re-delivering messages pasted with a previous contractual version or format during development.

Production

```csharp
mqtt.UseProductionDefaults();
```

Activa:

- MQTT 5;
- persistent session for 24 hours;
- exponential backoff with jitter;
- Last Will CloudEvent `UNAVAILABLE`;
- keep-alive for 30 seconds;
- timeout of 10 seconds;
- Package limit of 1 MiB;
- 32 pending QoS messages.

The mTLS configuration continues to be explicit because it needs a certificate:

```csharp
mqtt.UseMutualTls(mtls =>
{
    mtls.ClientCertificateProvider =
        new PfxCertificateProvider(
            "/run/secrets/equipment-worker.pfx",
            certificatePassword);

    mtls.ExpectedServerName =
        "mosquitto.enterprise.svc.internal";
    mtls.ExpectedClientIdentity =
        "equipment-worker";
});
```

It is not possible to disable server trust, string errors, or revocation.

### Brokerless Testing

```csharp
services.AddMqttReactiveOrm<TestMqttContext>(mqtt =>
{
    mqtt.UseInMemoryTransport()
        .ForModule("tests")
        .WithCloudEventSource("urn:tests:worker");

    mqtt.UseEventEntities(RegisterTestEventEntities);
    mqtt.UseSchemas(RegisterTestSchemas);
});
```

`InMemoryMqttBus` retains filters, backpressure, CloudEvents and contractual validation without starting Mosquitto.

## Fluent API Reference

All methods return the same builder and can be chained:

| Method | Effect |
|---|---|
| `ConnectTo(server, port, transport)` | Configure TCP or WebSocket |
| `IdentifyAs(clientId)` | Set ClientId stable |
| `UseMqtt5()` | Select MQTT 5 |
| `UseMqtt311()` | Activate MQTT 3.1.1 compatible mode |
| `ForModule(namespace)` | Limit all topics to module namespace |
| `WithCloudEventSource(uri)` | Defines the CloudEvents identity of the producer |
| `UseEventEntities(configure)` | Register Event Entities declared with attributes |
| `UseSchemas(configure)` | Log inline schemas or additional solvers |
| `UseSchemaResolver(resolver, capacity)` | Adds local/remote resolution with limited cache |
| `UseEventEntityPackage<T>()` | Import Event Entities and schemas from a generated package |
| `UsePersistentSession(expiry)` | Keep Broker Session and Subscriptions |
| `UseExponentialReconnect(initial, maximum)` | Configure jitter reconnection |
| `UseUnavailableLastWill(topic)` | Configure CloudEvent retained LWT |
| `UseMutualTls(configure)` | Enable TLS and Client Certificate |
| `UseInMemoryTransport()` | Replace MQTTnet with the test bus |
| `ForbidTopicValue(value)` | Prevents sensitive or instance data from appearing in topics |
| `UseDevelopmentDefaults()` | Apply local preset |
| `UseProductionDefaults()` | Apply operating preset |

`Advanced` exposes the complete options when a preset is not enough:

```csharp
mqtt.Advanced.KeepAlive = TimeSpan.FromSeconds(20);
mqtt.Advanced.Timeout = TimeSpan.FromSeconds(8);
mqtt.Advanced.ReceiveMaximum = 64;
mqtt.Advanced.MaximumPacketSize = 2 * 1024 * 1024;
mqtt.Advanced.Reconnect.JitterRatio = 0.25;
mqtt.Advanced.Reconnect.MaximumAttempts = 20;
mqtt.Advanced.Reconnect.MaximumDuration = TimeSpan.FromMinutes(15);
```

Configuration is validated while registering services. `ConnectTo`, `IdentifyAs`, `ForModule`, `WithCloudEventSource`, at least one Event Entity, and its schema are required for normal MQTT transport.

## Generated Event Entity packages

A NuGet package generated from the metamodel can implement:

```csharp
public sealed class EquipmentEventEntityPackage
    : IMqttEventEntityPackage
{
    public void Register(
        EventEntityRegistryBuilder eventEntities,
        MqttSchemaBuilder schemas)
    {
        eventEntities.Add<SensorReading>();

        schemas.AddInline(
            "urn:schema:factory:sensor-reading:v1",
            EmbeddedSchemas.SensorReadingV1,
            "1.0.0");
    }
}
```

The Worker only needs:

```csharp
builder.Services.AddMqttReactiveOrm<ApplicationMqttContext>(
    mqtt => mqtt
        .ConnectTo("mosquitto.enterprise.svc.internal", 8883)
        .IdentifyAs("equipment-worker")
        .ForModule("factory")
        .WithCloudEventSource("urn:factory:equipment-worker")
        .UseEventEntityPackage<EquipmentEventEntityPackage>()
        .UseProductionDefaults()
        .UseMutualTls(ConfigureMutualTls));
```

Thus, the Worker does not repeat `eventType`, `dataschema`, version or JSON Schema.

It is also possible to discover Event Entities carrying the standard attributes:

```csharp
mqtt.UseEventEntities(entities =>
    entities.AddEventEntities(
        typeof(SensorReading).Assembly));
```

## Dynamic Topics

```csharp
public sealed class SensorTopicResolver
    : ITopicResolver<SensorReading>
{
    public string ResolvePublishTopic(SensorReading data) =>
        $"factory/sensors/{Normalize(data.SensorId)}/events";

    public bool MatchesSubscription(string topic) =>
        topic.StartsWith(
            "factory/sensors/",
            StringComparison.Ordinal);
}
```

```csharp
[MqttTopic(
    SubscribeFilter = "factory/sensors/+/events",
    ResolverType = typeof(SensorTopicResolver),
    QoS = MqttQoS.AtLeastOnce)]
public TopicSet<SensorReading> SensorReadings =>
    Set<SensorReading>();
```

Record the resolution:

```csharp
builder.Services.AddSingleton<SensorTopicResolver>();
```

The topic produced by the resolver is validated in each publication.

## CloudEvents

All MQTT payloads are CloudEvents 1.0 structured JSON:

```json
{
  "specversion": "1.0",
  "id": "626ce1a8f33b45a59501af313bc34fd2",
  "source": "urn:factory:equipment-worker",
  "type": "com.factory.sensor.reading.v1",
  "subject": "sensor-42",
  "time": "2026-08-25T08:57:24.5489680+00:00",
  "datacontenttype": "application/json",
  "dataschema": "urn:schema:factory:sensor-reading:v1",
  "correlationid": "production-order-9138",
  "data": {
    "sensorId": "sensor-42",
    "temperature": 23.7,
    "humidity": 41.2
  }
}
```

MQTT 5 adds:

```text
Content Type: application/cloudevents+json; charset=utf-8
```

MQTT 3.1.1 carries the same JSON with implicit Content Type.

`MqttMessageContext<T>` states:

- `Data`;
- `CloudEvent`;
- `Identity`, formed by `source + id`;
- topic, QoS and retained;
- `AcknowledgeAsync()`.

Extensions available: `correlationid`, `causationid`, `traceparent`, `tracestate`, `negotiationid` and `expiresat`.

## Event Entities and schemas

Before publishing and before exposing `TData`, the library validates:

- envelope CloudEvents;
- `type` known;
- correspondence between `type`, `dataschema` and type C#;
- version compatibility;
Maximum Size
- prohibited fields;
- JSON Schema conformity.

Contract errors implement `INonRetryableError` with `IsRetryable = false`.

Available solvers:

- `InMemoryJsonSchemaResolver`;
- `FileJsonSchemaResolver`;
- `HttpJsonSchemaResolver`;
- `CompositeJsonSchemaResolver`;
- `CachingJsonSchemaResolver`.

Resolve external:

```csharp
mqtt.UseSchemaResolver(
    new HttpJsonSchemaResolver(httpClient),
    cacheCapacity: 128);
```

The common JSON profile uses camelCase, strict numbers, rejected unknown properties, and deterministic order.

The validator implements the profile that the SDK needs, not the entire 2020-12 JSON Schema Draft specification. It currently covers `type`, `required`, `properties`, `additionalProperties`, `items`, `enum`, string and number boundaries, and `#` local references. External combiners, formats and `$ref` must be resolved or standardized in the contract package before registering.

For Protobuf an explicit `IContractJsonMapper` must be provided.

## TLS and mTLS

`UseMutualTls` activates strict TLS and requires a client certificate. The provider can upload it from PFX, PEM, certificate store or a secret manager; this allows to keep non-exportable private keys when the underlying store or provider supports it.

```csharp
mqtt.ConnectTo("mosquitto.enterprise.svc.internal", 8883)
    .UseProductionDefaults()
    .UseMutualTls(mtls =>
    {
        mtls.ClientCertificateProvider =
            new StoreCertificateProvider(certificateThumbprint);
        mtls.ExpectedServerName =
            "mosquitto.enterprise.svc.internal";
        mtls.ExpectedClientIdentity = "equipment-worker";
        mtls.CheckCertificateRevocation = true;
    });
```

File providers monitor PFX/PEM changes. `SecretCertificateProvider.SignalRotation()` offers the same signal for a vault. The bus drains and reconnects to use the new certificate.

## Lifecycle, sessions and reconnection

```text
Created
  → Connecting
  → Connected
  → Subscribing
  → Ready
  → Reconnecting
  → Draining
  → Stopped
```

A terminal failure can take the bus to `Faulted`. `IMqttBus.IsReady` is only true in `Ready`. If the broker restores the persistent session, the subscriptions are not duplicated; if it does not restore it, they are re-registered.

```csharp
bus.StateChanged += (_, change) =>
    logger.LogInformation("MQTT {Previous} -> {Current}",
        change.Previous, change.Current);

if (!bus.IsReady)
    return HealthCheckResult.Unhealthy("MQTT no está preparado");
```

## Errors and acknowledgements

Envelope, contract, or schema failures are derived from `ContractValidationException` or implement `INonRetryableError`. This allows an external policy to send them to quarantine or DLQ without retrying a message that will never be valid.

```csharp
try
{
    await foreach (var message in topic.ReadAllAsync(cancellationToken))
    {
        await HandleAsync(message.Data, cancellationToken);
        await message.AcknowledgeAsync(cancellationToken);
    }
}
catch (Exception error) when
    (error is INonRetryableError { IsRetryable: false })
{
    await quarantine.StoreAsync(error, cancellationToken);
}
```

The SDK classifies the error, but does not yet incorporate a DLQ store, idempotent inbox, or SQL outbox. Those decisions require persistence and in-app transactions.

Tests

In addition to the in-memory bus, the repository includes an executable demo against Mosquitto. An integration test can boot the host, resolve the context, and use exactly the same calls as production:

```csharp
await context.SensorReadings.PublishAsync(reading, cancellationToken);

await foreach (var received in
    context.SensorReadings.ReadAllAsync(cancellationToken))
{
    Assert.Equal(reading.SensorId, received.Data.SensorId);
    await received.AcknowledgeAsync(cancellationToken);
    break;
}
```

Current Limits

- Does not include broker, bridge, Kafka Connect or access to Kafka.
- Does not yet implement inbox, outbox, persistent DLQ or capabilities protocol.
- Does not automatically deduplicate: exposes `message.Identity` (`source + id`) so that a transactional inbox can do it.
- Does not implement the full JSON Schema standard; it applies the profile documented in [Event Entities and schemas](#event-entities-and-schemas).
- Does not replace broker ACLs: local policy prevents errors, while Mosquitto retains final authority.
- Full OpenTelemetry is not yet integrated; `traceparent` and `tracestate` do propagate in CloudEvents.

Advanced Settings

The detailed API is still available via `Advanced`:

```csharp
mqtt.Advanced.ReceiveMaximum = 64;
mqtt.Advanced.MaximumPacketSize = 2 * 1024 * 1024;
mqtt.Advanced.Reconnect.JitterRatio = 0.25;
mqtt.Advanced.Reconnect.MaximumAttempts = 20;
```

Low-level registration extensions for special integrations also remain available.

 Demo

Start Mosquitto:

```bash
docker compose up -d
```

Perform:

```bash
dotnet run --project Demo/Demo.csproj
```

Stop with `Ctrl+C` to check the ordered closure.

### Unsupported previous payload

If `InvalidMqttCloudEventException` appears, the broker delivered a message that does not use CloudEvents structured JSON. `UseDevelopmentDefaults()` uses a clean session to discard QoS messages queued by previous versions.

A retained message belongs to the topic and survives even a clean session. It can be deleted by posting an empty retained payload:

```bash
mosquitto_pub -h localhost \
  -t factory_64/sensors/DHT230222_Modules/events \
  -r -n
```

In production, the message is classified as non-retryable by `INonRetryableError`; it is never interpreted as a valid payload métier.
