# Net.Mqtt.Infrastructure

## Presentación

Net.Mqtt.Infrastructure es el SDK MQTT común para Worker Services .NET. Conserva una idea deliberadamente pequeña —`topic MQTT ↔ TopicSet<TData> ↔ flujo tipado`— y centraliza alrededor de ella las obligaciones de una mensajería de producción: conexión, seguridad, CloudEvents, Event Entities, validación, resiliencia y consumo controlado.

El objetivo de la API pública es que el código de negocio trabaje con `TData` y `MqttMessageContext<TData>`, no con clientes MQTTnet, bytes, cabeceras ni lógica de reconexión.

## Descripción

La biblioteca se apoya en MQTTnet, pero no lo expone como dependencia del contexto de aplicación. Un único `IMqttBus` inyectable comparte la conexión dentro del Worker; `MqttOrmContext` declara el modelo de topics; y cada `TopicSet<TData>` publica y consume exclusivamente CloudEvents 1.0 validados mediante la Event Entity asociada al tipo C#.

```text
Topic MQTT
  → transporte seguro y resiliente
  → CloudEvent 1.0
  → Event Entity y JSON Schema
  → TopicSet<TData>
  → IAsyncEnumerable<MqttMessageContext<TData>>
```

La configuración habitual utiliza un único punto de entrada fluent. La biblioteca registra internamente MQTTnet, CloudEvents, Event Entities, schemas, topics, validadores y ciclo de vida.

## Propósito

Su propósito es ofrecer a todos los Workers la misma semántica técnica y evitar implementaciones diferentes para problemas transversales. Un productor no puede publicar un payload de negocio desnudo; un consumidor no recibe datos antes de validar su CloudEvent y su schema; y una entrega no se confirma hasta que la aplicación ejecuta `AcknowledgeAsync`.

La biblioteca está pensada para brokers MQTT internos, bridges controlados y contratos generados desde un metamodelo. Los Workers permanecen desacoplados de Kafka, de las bases de datos de otros servicios y de los detalles internos del transporte.

## Contenido

- [Presentación](#presentación)
- [Descripción](#descripción)
- [Propósito](#propósito)
- [Qué resuelve](#qué-resuelve)
- [Funcionalidades](#funcionalidades)
- [Inicio rápido](#inicio-rápido)
- [Ejemplos por funcionalidad](#ejemplos-por-funcionalidad)
- [Flujo de publicación y consumo](#flujo-de-publicación-y-consumo)
- [Presets](#presets)
- [Referencia de la API fluent](#referencia-de-la-api-fluent)
- [Paquetes de Event Entities generados](#paquetes-de-event-entities-generados)
- [Topics dinámicos](#topics-dinámicos)
- [CloudEvents](#cloudevents)
- [Event Entities y schemas](#event-entities-y-schemas)
- [TLS y mTLS](#tls-y-mtls)
- [Ciclo de vida, sesiones y reconexión](#ciclo-de-vida-sesiones-y-reconexión)
- [Errores y acknowledgements](#errores-y-acknowledgements)
- [Pruebas](#pruebas)
- [Límites actuales](#límites-actuales)

## Qué resuelve

La biblioteca centraliza las decisiones técnicas que de otro modo acabarían repetidas en cada Worker:

| Área | Responsabilidad |
|---|---|
| Transporte | Conexión MQTTnet compartida e inyectable |
| Ciclo de vida | Inicio y cierre coordinados con Generic Host |
| Resiliencia | Sesiones persistentes, reconexión y resuscripción |
| Seguridad | TLS/mTLS estricto y rotación de certificados |
| Envelope | CloudEvents 1.0 structured JSON obligatorio |
| Event Entities | Relación entre `type`, `dataschema`, versión y tipo C# |
| Validación | Límites, campos prohibidos y perfil JSON Schema |
| Topics | Separación entre publicación y filtro de suscripción |
| Consumo | Cancelación, backpressure y acknowledgement explícito |
| Pruebas | Transporte en memoria sin broker real |

No es un broker ni un reemplazo de MQTTnet. Es una capa de aplicación que impone un protocolo común encima de MQTTnet.

## Funcionalidades

| Funcionalidad | Resultado |
|---|---|
| Transporte inyectable | `IMqttBus` aísla MQTTnet y permite sustituirlo por un bus en memoria |
| API asíncrona | Publicación, lectura, conexión, desconexión y acknowledgement aceptan `CancellationToken` |
| MQTT 5 y 3.1.1 | MQTT 5 es el modo preferido; MQTT 3.1.1 conserva el payload CloudEvents estructurado |
| Ciclo de vida | Máquina de estados, sesiones persistentes, reconexión con jitter, resuscripción y LWT |
| TLS y mTLS | Confianza estricta del servidor, nombre esperado, revocación, identidad cliente y rotación |
| CloudEvents tipado | Envelope 1.0 obligatorio; `TopicSet<TData>` representa únicamente `data` |
| Event Entities | Asociación única entre tipo C#, `type`, `dataschema` y versión |
| Validación | Tamaño, campos prohibidos, perfil JSON común y subconjunto JSON Schema |
| Modelo de topics | Diferencia explícita entre topic de publicación y filtro de suscripción |
| Consumo controlado | `IAsyncEnumerable`, canal acotado, backpressure y acknowledgement después del procesamiento |
| Compatibilidad Rx | `IObservable<T>` se conserva para migraciones y escenarios sin acknowledgement manual |
| Testabilidad | `InMemoryMqttBus` ejecuta el pipeline sin un broker real |

Las secciones [Inicio rápido](#inicio-rápido) y [Ejemplos por funcionalidad](#ejemplos-por-funcionalidad) muestran la implementación de todas estas capacidades.

## Instalación

```bash
dotnet add package Net.Mqtt.Infrastructure
```

Requisitos:

- .NET 10
- Broker MQTT 5 o MQTT 3.1.1

## Inicio rápido

### 1. Event Entity

Una **Event Entity** es el estándar de la biblioteca para representar en CLR los datos transportados por un CloudEvent. Su identidad y sus políticas se declaran exclusivamente mediante atributos en la propia clase.

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

### 2. Contexto MQTT

El contexto solo declara sus topics. No construye conexiones ni registra Event Entities:

```csharp
using Net.Mqtt.Infrastructure;
using Net.Mqtt.Infrastructure.Attributes;
using Net.Mqtt.Infrastructure.Enums;

public sealed class ApplicationMqttContext(
    MqttContextDependencies dependencies)
    : MqttOrmContext(dependencies)
{
    [MqttTopic(
        PublishTopic = "sensors/readings/events",
        SubscribeFilter = "sensors/+/events",
        QoS = MqttQoS.AtLeastOnce,
        Retain = false)]
    public TopicSet<SensorReading> SensorReadings =>
        Set<SensorReading>();
}
```

`PublishTopic` nunca admite `+`, `#` ni `@`. `SubscribeFilter` permite los wildcards MQTT válidos `+` y `#`.

### 3. Configuración fluent

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMqttReactiveOrm<ApplicationMqttContext>(mqtt =>
{
    mqtt.ConnectTo("localhost", 1883)
        .IdentifyAs("equipment-worker")
        .WithBaseTopic("factory")
        .ForModule("factory")
        .ForService("equipment-worker")
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

Esta única llamada registra:

- `IMqttBus` compartido;
- `MqttNetBus`;
- `ITopicModel`;
- `ICloudEventFactory` y `ICloudEventCodec`;
- `IEventEntityRegistry`;
- resolutor, caché y validador JSON Schema;
- `MqttContextDependencies`;
- `ApplicationMqttContext`;
- servicio de conexión y cierre MQTT.

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

Publicación:

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

## Ejemplos por funcionalidad

### Transporte inyectable y bus compartido

El contexto recibe todas sus dependencias por DI. No crea un cliente MQTTnet ni abre una conexión propia:

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

Todas las instancias del contexto y los servicios del Worker utilizan el mismo `IMqttBus`. En pruebas, `UseInMemoryTransport()` sustituye la implementación sin cambiar el contexto ni el consumidor.

### MQTT 5, MQTT 3.1.1, TCP y WebSocket

MQTT 5 sobre TCP es la opción recomendada:

```csharp
mqtt.ConnectTo("mosquitto.internal", 8883)
    .UseMqtt5();
```

Para interoperar con un broker antiguo:

```csharp
mqtt.ConnectTo("legacy-broker.internal", 1883)
    .UseMqtt311();
```

Para WebSocket, además del transporte se indica la URI completa:

```csharp
mqtt.ConnectTo("mosquitto.internal", 443, MqttTransport.WebSocket);
mqtt.Advanced.WebSocketUri = "wss://mosquitto.internal/mqtt";
```

El envelope CloudEvents structured JSON es obligatorio en las dos versiones del protocolo.

### Sesión persistente, reconexión y Last Will

```csharp
mqtt.UsePersistentSession(TimeSpan.FromHours(24))
    .UseExponentialReconnect(
        initial: TimeSpan.FromSeconds(1),
        maximum: TimeSpan.FromMinutes(1))
    .UseUnavailableLastWill("factory/services/equipment-worker/status");
```

MQTT 5 usa `Clean Start` y `Session Expiry Interval`; MQTT 3.1.1 usa `Clean Session = false`. Cuando el broker no restaura la sesión, el bus restaura automáticamente sus suscripciones.

### Estado, readiness y sesión restaurada

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

La readiness debe depender de `IsReady`, no solo de que exista un socket conectado.

### TLS/mTLS y rotación de certificados

Desde un PFX montado como secreto:

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

También existen proveedores PEM, certificate store y secretos externos:

```csharp
var pem = new PemCertificateProvider("worker.crt", "worker.key");
var store = new StoreCertificateProvider(certificateThumbprint);
var secret = new SecretCertificateProvider(
    token => vault.GetCertificateAsync("equipment-worker", token));

secret.SignalRotation(); // Notifica el cambio y fuerza una reconexión segura.
```

La expiración se puede observar sin acceder al certificado privado:

```csharp
bus.CertificateExpiring += (_, warning) =>
    logger.LogWarning("El certificado {Subject} vence en {Remaining}",
        warning.Subject, warning.Remaining);
```

La cadena del servidor, DNS/SAN, periodo de validez y revocación se validan siempre. Las opciones permisivas no forman parte de la API de producción.

### CloudEvents y correlación

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

El SDK genera `specversion`, `id`, `source`, `type`, `datacontenttype`, `dataschema` y `time`. La identidad idempotente que expone el mensaje es el par `source + id`, nunca `id` por separado.

### Event Entity, versión y JSON Schema

```csharp
mqtt.UseEventEntities(entities => entities.Add<SensorReading>());

mqtt.UseSchemas(schemas => schemas.AddInline(
    "urn:schema:factory:sensor-reading:v1",
    SensorSchemas.ReadingV1,
    "1.0.0"));
```

`EventType`, `DataSchema` y `EventVersion` son obligatorios. `MaximumDataSize`,
`SchemaCompatibility`, `CompatibleDataSchema`, `ForbiddenField` y
`ContractJsonMapper` son opcionales. `Add<T>()` lanza una excepción explícita
que identifica el tipo y los atributos obligatorios ausentes o inválidos.

La validación se ejecuta antes de publicar y después de recibir, pero antes de exponer `TData`.

### Schemas locales, remotos y caché

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

El resolutor HTTP solo debe apuntar a un repositorio contractual confiable. La caché está acotada para que un conjunto no controlado de URIs no aumente indefinidamente la memoria.

### Proyección explícita Protobuf → JSON

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

No se infiere una conversión Protobuf arbitraria: el paquete contractual gobierna la representación JSON que se valida y transporta.

### Topics estáticos y dinámicos

`WithBaseTopic` permite que todos los valores de `[MqttTopic]` sean relativos. El prefijo se aplica tanto al publicar como al crear la suscripción:

```csharp
mqtt.ConnectTo(mqttHost, mqttPort)
    .IdentifyAs(identity)
    .WithBaseTopic("mint/v1.2.55")
    .ForModule("mint_module_business1")
    .ForService("mint_webservice_business1")
    .WithCloudEventSource($"urn:mint_module_business1:{identity}");

[MqttTopic(
    PublishTopic = "events/created",
    SubscribeFilter = "events/+")]
public TopicSet<BusinessEvent> Events => Set<BusinessEvent>();
```

Los topics efectivos serán `mint/v1.2.55/moduls/mint_module_business1/services/mint_webservice_business1/events/created` y `mint/v1.2.55/moduls/mint_module_business1/services/mint_webservice_business1/events/+`. La biblioteca construye la jerarquía fija `base/moduls/{module}/services/{service}`, normaliza las barras y valida que las identidades sean segmentos MQTT simples.

Los segmentos `../` permiten publicar o suscribirse en una jerarquía superior. La navegación parte de la raíz del servicio y se aplica de igual forma a `PublishTopic`, `SubscribeFilter` y topics dinámicos:

```csharp
[MqttTopic(
    PublishTopic = "../../capabilities/simple_message/request",
    SubscribeFilter = "../../capabilities/simple_message/request")]
public TopicSet<SimpleMessage> SimpleMessages => Set<SimpleMessage>();
```

Con la configuración anterior se resuelve como `mint/v1.2.55/moduls/mint_module_business1/capabilities/simple_message/request`: el primer `..` elimina la identidad del servicio y el segundo elimina el segmento `services`. La librería rechaza cualquier ruta que intente escapar por encima de `WithBaseTopic`.

Los segmentos de versión de la raíz pueden usar el formato `v` seguido de componentes separados por puntos, por ejemplo `v1.2.55` o `v0.0.0.a`. Estos valores no se confunden con hostnames; los segmentos que realmente tienen forma de hostname continúan prohibidos.

Un topic estático se declara una sola vez en `[MqttTopic]`; el registro de Event Entities aporta `type` y `dataschema`:

```csharp
[MqttTopic(
    PublishTopic = "sensors/readings/events",
    SubscribeFilter = "sensors/+/events",
    QoS = MqttQoS.AtLeastOnce)]
public TopicSet<SensorReading> SensorReadings => Set<SensorReading>();
```

Para topics calculados, se omite `PublishTopic` y se utiliza `ResolverType`; hay un ejemplo completo en [Topics dinámicos](#topics-dinámicos).

### Consumo asíncrono, backpressure y acknowledgement

```csharp
await foreach (var message in context.SensorReadings.ReadAllAsync(
    new SubscriptionOptions { Capacity = 32 }, cancellationToken))
{
    await handler.HandleAsync(message.Data, cancellationToken);
    await message.AcknowledgeAsync(cancellationToken);
}
```

El canal acotado frena la lectura cuando el consumidor se retrasa. Si el handler falla, no se ejecuta el acknowledgement y MQTT puede volver a entregar el mensaje según su QoS y sesión.

### HTTP → MQTT request/reply

El contexto puede declarar un dispatcher request/reply compartido:

```csharp
using Net.Mqtt.Infrastructure.RequestReply;

public MqttRequestSet<SimpleMessage, SempleMessageResponse> SimpleMessageRequest =>
    Request<SimpleMessage, SempleMessageResponse>(
        nameof(SempleMessage),
        nameof(SempleMessageResponse));
```

Los argumentos son los nombres de las dos propiedades `TopicSet<T>` del contexto. El endpoint HTTP queda reducido a una operación:

```csharp
var response = await context.SimpleMessageRequest.SendAsync(
    new SimpleMessage { Message = message },
    new MqttRequestOptions { Timeout = TimeSpan.FromSeconds(10) },
    cancellationToken);

return Results.Ok(response.Data);
```

La librería crea el `correlationid`, instala una única suscripción compartida, espera el `SUBACK`, publica, despacha la respuesta al caller correcto y ejecuta su ACK. Las peticiones HTTP concurrentes no crean una suscripción MQTT cada una.

El Worker respondedor puede copiar automáticamente correlación, causación y tracing:

```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
    context.SimpleMessageRequest.HandleAsync(
        (request, cancellationToken) => Task.FromResult(
            new SempleMessageResponse { Response = "OK" }),
        stoppingToken);
```

El timeout produce `MqttRequestTimeoutException`. El endpoint no gestiona `MoveNextAsync` ni `DisposeAsync`.

### Compatibilidad reactiva

```csharp
using var subscription = context.SensorReadings
    .Where(reading => reading.Temperature > 30)
    .Subscribe(reading => logger.LogWarning(
        "Temperatura alta: {Temperature}", reading.Temperature));
```

Rx se mantiene para compatibilidad y flujos simples. Para Workers se recomienda `ReadAllAsync`, porque propaga cancelación, aplica backpressure y entrega el contexto necesario para confirmar después del resultado de negocio.

### Pruebas sin Mosquitto

```csharp
services.AddMqttReactiveOrm<TestMqttContext>(mqtt =>
{
    mqtt.UseInMemoryTransport()
        .WithBaseTopic("test")
        .ForModule("tests")
        .ForService("sensor-worker")
        .WithCloudEventSource("urn:tests:sensor-worker");
    mqtt.UseEventEntities(RegisterEventEntities);
    mqtt.UseSchemas(RegisterSchemas);
});
```

El mismo test puede resolver `TestMqttContext`, publicar y leer mediante la API real, sin puertos, contenedores ni esperas de red.

## Flujo de publicación y consumo

### Publicación

```text
TData
  → resolver la Event Entity por tipo C#
  → comprobar CloudEvent type + dataschema
  → serializar con el perfil JSON determinista
  → validar tamaño y campos prohibidos
  → validar JSON Schema
  → crear CloudEvent 1.0
  → resolver y validar PublishTopic
  → publicar mediante IMqttBus
```

La publicación se detiene antes de tocar el broker si el contrato o el schema no son válidos.

Para añadir metadatos técnicos:

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

Si existe una `Activity` activa, `traceparent` y `tracestate` se copian automáticamente cuando no se especifican manualmente.

### Consumo

```text
MqttDelivery
  → validar Content Type y envelope CloudEvents
  → resolver la Event Entity por CloudEvent type
  → comprobar dataschema + versión + TData
  → validar límites y JSON Schema sobre data sin deserializarla
  → deserializar TData
  → entregar MqttMessageContext<TData>
  → procesar
  → acknowledge
```

La capacidad predeterminada de una suscripción es 128 mensajes. Puede ajustarse para aplicar backpressure:

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

## Presets

### Desarrollo

```csharp
mqtt.UseDevelopmentDefaults();
```

Activa MQTT 5, sesión limpia sin persistencia y reconexión rápida. Esto evita que Mosquitto reentregue durante el desarrollo mensajes encolados con una versión contractual o formato anterior.

### Producción

```csharp
mqtt.UseProductionDefaults();
```

Activa:

- MQTT 5;
- sesión persistente durante 24 horas;
- backoff exponencial con jitter;
- Last Will CloudEvent `UNAVAILABLE`;
- keep-alive de 30 segundos;
- timeout de 10 segundos;
- límite de paquete de 1 MiB;
- 32 mensajes QoS pendientes.

La configuración mTLS continúa siendo explícita porque necesita un certificado:

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

No es posible desactivar la confianza del servidor, los errores de cadena ni la revocación.

### Pruebas sin broker

```csharp
services.AddMqttReactiveOrm<TestMqttContext>(mqtt =>
{
    mqtt.UseInMemoryTransport()
        .WithBaseTopic("test")
        .ForModule("tests")
        .ForService("worker")
        .WithCloudEventSource("urn:tests:worker");

    mqtt.UseEventEntities(RegisterTestEventEntities);
    mqtt.UseSchemas(RegisterTestSchemas);
});
```

`InMemoryMqttBus` conserva filtros, backpressure, CloudEvents y validación contractual sin iniciar Mosquitto.

## Referencia de la API fluent

Todos los métodos devuelven el mismo builder y pueden encadenarse:

| Método | Efecto |
|---|---|
| `ConnectTo(server, port, transport)` | Configura TCP o WebSocket |
| `IdentifyAs(clientId)` | Establece el ClientId estable |
| `WithBaseTopic(topic)` | Prefija los topics de publicación y filtros de suscripción relativos |
| `UseMqtt5()` | Selecciona MQTT 5 |
| `UseMqtt311()` | Activa el modo compatible MQTT 3.1.1 |
| `ForModule(identity)` | Define la identidad del módulo como un único segmento MQTT |
| `ForService(identity)` | Define la identidad del servicio como un único segmento MQTT |
| `WithCloudEventSource(uri)` | Define la identidad CloudEvents del productor |
| `UseEventEntities(configure)` | Registra Event Entities declaradas mediante atributos |
| `UseSchemas(configure)` | Registra schemas inline o resolutores adicionales |
| `UseSchemaResolver(resolver, capacity)` | Añade resolución local/remota con caché limitada |
| `UseEventEntityPackage<T>()` | Importa Event Entities y schemas de un paquete generado |
| `UsePersistentSession(expiry)` | Conserva sesión y suscripciones en el broker |
| `UseExponentialReconnect(initial, maximum)` | Configura reconexión con jitter |
| `UseUnavailableLastWill(topic)` | Configura el LWT CloudEvent retained |
| `UseMutualTls(configure)` | Activa TLS y certificado cliente |
| `UseInMemoryTransport()` | Sustituye MQTTnet por el bus de pruebas |
| `ForbidTopicValue(value)` | Impide que un dato sensible o de instancia aparezca en topics |
| `UseDevelopmentDefaults()` | Aplica el preset local |
| `UseProductionDefaults()` | Aplica el preset operativo |

`Advanced` expone las opciones completas cuando un preset no basta:

```csharp
mqtt.Advanced.KeepAlive = TimeSpan.FromSeconds(20);
mqtt.Advanced.Timeout = TimeSpan.FromSeconds(8);
mqtt.Advanced.ReceiveMaximum = 64;
mqtt.Advanced.MaximumPacketSize = 2 * 1024 * 1024;
mqtt.Advanced.Reconnect.JitterRatio = 0.25;
mqtt.Advanced.Reconnect.MaximumAttempts = 20;
mqtt.Advanced.Reconnect.MaximumDuration = TimeSpan.FromMinutes(15);
```

La configuración se valida al registrar los servicios. `ConnectTo`, `IdentifyAs`, `ForModule`, `WithCloudEventSource`, al menos una Event Entity y su schema son necesarios para el transporte MQTT habitual.

## Paquetes de Event Entities generados

Un paquete NuGet generado desde el metamodelo puede implementar:

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

El Worker solo necesita:

```csharp
builder.Services.AddMqttReactiveOrm<ApplicationMqttContext>(
    mqtt => mqtt
        .ConnectTo("mosquitto.enterprise.svc.internal", 8883)
        .IdentifyAs("equipment-worker")
        .WithBaseTopic("factory")
        .ForModule("factory")
        .ForService("equipment-worker")
        .WithCloudEventSource("urn:factory:equipment-worker")
        .UseEventEntityPackage<EquipmentEventEntityPackage>()
        .UseProductionDefaults()
        .UseMutualTls(ConfigureMutualTls));
```

Así, el Worker no repite `eventType`, `dataschema`, versión ni JSON Schema.

También es posible descubrir Event Entities que tengan los atributos estándar:

```csharp
mqtt.UseEventEntities(entities =>
    entities.AddEventEntities(
        typeof(SensorReading).Assembly));
```

## Topics dinámicos

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

Registrar el resolver:

```csharp
builder.Services.AddSingleton<SensorTopicResolver>();
```

El topic producido por el resolver se valida en cada publicación.

## CloudEvents

Todos los payloads MQTT son CloudEvents 1.0 structured JSON:

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

MQTT 5 añade:

```text
Content Type: application/cloudevents+json; charset=utf-8
```

MQTT 3.1.1 transporta el mismo JSON con Content Type implícito.

`MqttMessageContext<T>` expone:

- `Data`;
- `CloudEvent`;
- `Identity`, formada por `source + id`;
- topic, QoS y retained;
- `AcknowledgeAsync()`.

Extensiones disponibles: `correlationid`, `causationid`, `traceparent`, `tracestate`, `negotiationid` y `expiresat`.

## Event Entities y schemas

Antes de publicar y antes de exponer `TData`, la biblioteca valida:

- envelope CloudEvents;
- `type` conocido;
- correspondencia entre `type`, `dataschema` y tipo C#;
- compatibilidad de versión;
- tamaño máximo;
- campos prohibidos;
- conformidad JSON Schema.

Los errores contractuales implementan `INonRetryableError` con `IsRetryable = false`.

Resolutores disponibles:

- `InMemoryJsonSchemaResolver`;
- `FileJsonSchemaResolver`;
- `HttpJsonSchemaResolver`;
- `CompositeJsonSchemaResolver`;
- `CachingJsonSchemaResolver`.

Resolver externo:

```csharp
mqtt.UseSchemaResolver(
    new HttpJsonSchemaResolver(httpClient),
    cacheCapacity: 128);
```

El perfil JSON común usa camelCase, números estrictos, propiedades desconocidas rechazadas y orden determinista.

El validador implementa el perfil que necesita el SDK, no toda la especificación JSON Schema Draft 2020-12. Actualmente cubre `type`, `required`, `properties`, `additionalProperties`, `items`, `enum`, límites de cadenas y números, y referencias locales `#`. Los combinadores, formatos y `$ref` externos deben resolverse o normalizarse en el paquete contractual antes de registrarlo.

Para Protobuf debe proporcionarse un `IContractJsonMapper` explícito.

## TLS y mTLS

`UseMutualTls` activa TLS estricto y requiere un certificado cliente. El proveedor puede cargarlo desde PFX, PEM, certificate store o un gestor de secretos; esto permite conservar claves privadas no exportables cuando el store o proveedor subyacente lo soporte.

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

Los proveedores de archivo vigilan cambios de PFX/PEM. `SecretCertificateProvider.SignalRotation()` ofrece la misma señal para un vault. El bus drena y reconecta para utilizar el certificado nuevo.

## Ciclo de vida, sesiones y reconexión

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

Un fallo terminal puede llevar el bus a `Faulted`. `IMqttBus.IsReady` solo es verdadero en `Ready`. Si el broker restaura la sesión persistente, las suscripciones no se duplican; si no la restaura, se registran nuevamente.

```csharp
bus.StateChanged += (_, change) =>
    logger.LogInformation("MQTT {Previous} -> {Current}",
        change.Previous, change.Current);

if (!bus.IsReady)
    return HealthCheckResult.Unhealthy("MQTT no está preparado");
```

## Errores y acknowledgements

Los fallos de envelope, contrato o schema derivan de `ContractValidationException` o implementan `INonRetryableError`. Esto permite que una política externa los envíe a cuarentena o DLQ sin reintentar un mensaje que nunca será válido.

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

El SDK clasifica el error, pero todavía no incorpora un almacén DLQ, inbox idempotente ni outbox SQL. Esas decisiones requieren persistencia y transacciones propias de la aplicación.

## Pruebas

Además del bus en memoria, el repositorio incluye una demo ejecutable contra Mosquitto. Un test de integración puede arrancar el host, resolver el contexto y usar exactamente las mismas llamadas que producción:

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

## Límites actuales

- No incluye broker, bridge, Kafka Connect ni acceso a Kafka.
- No implementa todavía inbox, outbox, DLQ persistente ni protocolo de capacidades.
- No deduplica automáticamente: expone `message.Identity` (`source + id`) para que una inbox transaccional pueda hacerlo.
- No implementa el estándar JSON Schema completo; aplica el perfil documentado en [Event Entities y schemas](#event-entities-y-schemas).
- No sustituye las ACL del broker: la política local previene errores, mientras Mosquitto conserva la autoridad final.
- OpenTelemetry completo aún no está integrado; `traceparent` y `tracestate` sí se propagan en CloudEvents.

## Configuración avanzada

La API detallada sigue disponible mediante `Advanced`:

```csharp
mqtt.Advanced.ReceiveMaximum = 64;
mqtt.Advanced.MaximumPacketSize = 2 * 1024 * 1024;
mqtt.Advanced.Reconnect.JitterRatio = 0.25;
mqtt.Advanced.Reconnect.MaximumAttempts = 20;
```

También permanecen disponibles las extensiones de registro de bajo nivel para integraciones especiales.

## Demo

El proyecto `DemoApiExpose` muestra el puente HTTP → MQTT request/reply con dos estilos ASP.NET Core: controller y Minimal API. Usa el transporte in-memory por defecto:

```bash
dotnet run --project DemoApiExpose/DemoApiExpose.csproj --urls http://localhost:5080
```

Endpoints: `POST /api/controller/messages` y `POST /api/minimal/messages`, ambos con body `{ "message": "hello" }`.

Para verificar sin broker el flujo completo, incluido request/reply con tres solicitudes concurrentes y correlación automática:

```bash
dotnet run --project Demo/Demo.csproj -- --in-memory
```

La salida confirma `ACK:alpha`, `ACK:beta` y `ACK:gamma` con un `correlationid` distinto para cada petición.

Iniciar Mosquitto:

```bash
docker compose up -d
```

Ejecutar:

```bash
dotnet run --project Demo/Demo.csproj
```

Detener con `Ctrl+C` para comprobar el cierre ordenado.

### Payload anterior no compatible

Si aparece `InvalidMqttCloudEventException`, el broker entregó un mensaje que no usa CloudEvents structured JSON. `UseDevelopmentDefaults()` utiliza una sesión limpia para descartar mensajes QoS encolados por versiones anteriores.

Un mensaje retained pertenece al topic y sobrevive incluso a una sesión limpia. Puede eliminarse publicando un payload vacío retained:

```bash
mosquitto_pub -h localhost \
  -t factory_64/sensors/DHT230222_Modules/events \
  -r -n
```

En producción, el mensaje se clasifica como no retryable mediante `INonRetryableError`; no se interpreta nunca como un payload métier válido.
