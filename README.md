# Net.Mqtt.ReactiveOrm

SDK MQTT tipado, asíncrono y reactivo para Worker Services .NET.

Net.Mqtt.ReactiveOrm mantiene un modelo sencillo:

```text
Topic MQTT <-> TopicSet<T> <-> IAsyncEnumerable<T> / IObservable<T>
```

La versión 2 convierte ese modelo en una infraestructura apta para servicios de larga duración: transporte inyectable, API cancelable, backpressure, acknowledgements después del procesamiento, sesiones persistentes, reconexión automática, Last Will y ciclo de vida integrado con Generic Host.

## Estado actual

La biblioteca incluye:

- `IMqttBus` como frontera pública del transporte.
- `MqttNetBus` como implementación basada en MQTTnet.
- `InMemoryMqttBus` para pruebas sin broker.
- `MqttOrmContext` sin construcción oculta del cliente MQTT.
- Registro explícito de topics mediante `TopicModelBuilder`.
- Publicación y consumo completamente asíncronos y cancelables.
- CloudEvents 1.0 tipados en structured content mode JSON para todos los mensajes.
- Consumo recomendado mediante `IAsyncEnumerable<MqttMessageContext<T>>`.
- Compatibilidad con Reactive Extensions mediante `IObservable<T>`.
- Canales acotados para aplicar backpressure.
- Acknowledgement explícito después del procesamiento.
- MQTT 5 como protocolo predeterminado y compatibilidad MQTT 3.1.1.
- Sesiones persistentes y detección de sesiones restauradas.
- Reconexión con backoff exponencial y jitter.
- Restauración automática de suscripciones cuando el broker no restaura la sesión.
- Máquina de estados observable y señal de readiness.
- Last Will CloudEvent `UNAVAILABLE`.
- TLS/mTLS Zero Trust avec validation stricte et rotation de certificats.
- Integración con Generic Host e inyección de dependencias.

> La API 2.0 es incompatible con la API 1.x. Ya no existen `Publish()`, `Unsubscribe()`, constructores de contexto con host/puerto ni inicialización de propiedades mediante reflexión.

## Requisitos

- .NET 10
- Un broker compatible con MQTT 5 o MQTT 3.1.1

Para ejecutar la demo se puede iniciar Mosquitto con:

```bash
docker compose up -d
```

## Instalación

```bash
dotnet add package Net.Mqtt.ReactiveOrm
```

Durante el desarrollo también puede utilizarse una referencia directa al proyecto:

```xml
<ProjectReference Include="..\Net.Mqtt.ReactiveOrm\Net.Mqtt.ReactiveOrm.csproj" />
```

## 1. Definir el contrato de datos

```csharp
public sealed class SensorReading
{
    public required string SensorId { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

`TopicSet<TData>` representa exclusivamente el tipo de `data`. La biblioteca lo envuelve siempre en un CloudEvent 1.0 structured JSON mediante `ICloudEventFactory` e `ICloudEventCodec`. Un payload métier JSON sin envelope CloudEvents no es válido.

## 2. Registrar explícitamente el modelo de topics

El contexto no inspecciona ni modifica propiedades derivadas durante su construcción. Cada `TopicSet<T>` se resuelve desde un modelo explícito e inmutable:

```csharp
using Net.Mqtt.ReactiveOrm;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Models;
using Net.Mqtt.ReactiveOrm.CloudEvents;

public sealed class ApplicationMqttContext(
    IMqttBus bus,
    ITopicModel model) : MqttOrmContext(bus, model)
{
    public TopicSet<SensorReading> SensorReadings => Set<SensorReading>();

    public static TopicModel CreateModel() => new TopicModelBuilder()
        .Add<SensorReading>(
            nameof(SensorReadings),
            "factory/sensors/SensorReading/events",
            QoSLevel.AtLeastOnce,
            cloudEvent: new CloudEventDescriptor(
                Source: new Uri("urn:factory:equipment-worker"),
                Type: "com.factory.sensor.reading.v1",
                DataSchema: new Uri("urn:schema:factory:sensor-reading:v1")))
        .Build();
}
```

El nombre usado en `TopicModelBuilder.Add<T>()` debe coincidir con el nombre de la propiedad. `Set<T>()` obtiene ese nombre mediante `CallerMemberName`, sin reflexión.

La marca `@` de una plantilla se sustituye por el nombre del tipo:

```csharp
.Add<SensorReading>(
    nameof(SensorReadings),
    "factory/sensors/@/events")
```

## 3. Configurar MQTT y Generic Host

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet.Formatter;
using Net.Mqtt.ReactiveOrm.Models;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMqttReactiveOrm(options =>
{
    options.ProtocolVersion = MqttProtocolVersion.V500;
    options.Server = "mosquitto.enterprise.svc.internal";
    options.Port = 1883;
    options.Transport = MqttTransport.Tcp;
    options.ClientId = "enterprise-equipment-worker";

    options.KeepAlive = TimeSpan.FromSeconds(30);
    options.Timeout = TimeSpan.FromSeconds(10);
    options.MaximumPacketSize = 1024 * 1024;
    options.ReceiveMaximum = 32;

    options.Session.CleanStart = false;
    options.Session.Expiry = TimeSpan.FromHours(24);

    options.Reconnect.UseExponentialBackoff(
        initialDelay: TimeSpan.FromSeconds(1),
        maximumDelay: TimeSpan.FromSeconds(30));
    options.Reconnect.MaximumAttempts = 20;
    options.Reconnect.MaximumDuration = TimeSpan.FromMinutes(10);

    options.LastWill.MessageExpiry = TimeSpan.FromMinutes(5);
    options.LastWill.UseServiceUnavailableCloudEvent();
});

builder.Services.AddSingleton<ITopicModel>(
    _ => ApplicationMqttContext.CreateModel());
builder.Services.AddSingleton<ApplicationMqttContext>();
builder.Services.AddHostedService<SensorWorker>();

await builder.Build().RunAsync();
```

`AddMqttReactiveOrm` registra un único `IMqttBus` compartido. Un servicio hospedado conecta el bus durante el arranque y realiza un cierre ordenado cuando el Host recibe su señal de parada.

### WebSocket

```csharp
options.Transport = MqttTransport.WebSocket;
options.WebSocketUri = "ws://localhost:9001/mqtt";
```

### MQTT 3.1.1

```csharp
options.ProtocolVersion = MqttProtocolVersion.V311;
```

En modo MQTT 3.1.1 la biblioteca usa `CleanSession = false`. En MQTT 5 usa `CleanStart` y `Session.Expiry`.

## 4. Consumir con cancelación, backpressure y acknowledgement

`ReadAllAsync` es la API recomendada para Workers:

```csharp
using Microsoft.Extensions.Hosting;
using Net.Mqtt.ReactiveOrm.Models;

public sealed class SensorWorker(
    ApplicationMqttContext context) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var options = new SubscriptionOptions
        {
            Capacity = 64
        };

        await foreach (var message in context.SensorReadings.ReadAllAsync(
            options,
            stoppingToken))
        {
            await ProcessAsync(message.Data, stoppingToken);

            // Solo confirmar después de que el procesamiento termine correctamente.
            await message.AcknowledgeAsync(stoppingToken);
        }
    }

    private static Task ProcessAsync(
        SensorReading reading,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"{reading.SensorId}: {reading.Temperature} °C");
        return Task.CompletedTask;
    }
}
```

`MqttMessageContext<T>` expone:

- `Data`: objeto deserializado.
- `CloudEvent`: envelope tipado completo.
- `Identity`: identidad compuesta por `Source` e `Id`.
- `Topic`: topic MQTT real que recibió el mensaje.
- `QoS`: nivel de entrega.
- `Retain`: indica si el mensaje fue entregado como retained.
- `IsAcknowledged`: estado local del acknowledgement.
- `AcknowledgeAsync()`: confirmación idempotente.

La capacidad de `SubscriptionOptions` crea un canal acotado. Cuando el consumidor no puede mantener el ritmo, la lectura MQTT espera en lugar de acumular mensajes indefinidamente.

## 5. Publicar

```csharp
await context.SensorReadings.PublishAsync(
    new SensorReading
    {
        SensorId = "sensor-42",
        Temperature = 23.7,
        Humidity = 41.2
    },
    new CloudEventPublishOptions
    {
        Context = new CloudEventPublishContext
        {
            Subject = "sensor-42",
            Extensions = new CloudEventExtensions
            {
                CorrelationId = correlationId,
                CausationId = causationId
            }
        }
    },
    stoppingToken);
```

Es posible sobrescribir QoS y retained para una publicación concreta:

```csharp
await context.SensorReadings.PublishAsync(
    reading,
    new CloudEventPublishOptions
    {
        QoS = QoSLevel.ExactlyOnce,
        Retain = false
    },
    stoppingToken);
```

`PublishAsync` propaga la cancelación y lanza una excepción si MQTTnet informa que la publicación no fue aceptada.

## CloudEvents 1.0 tipados

Todo mensaje aplicativo se transporta en structured content mode JSON. En MQTT 5 la publicación incluye:

```text
Content Type: application/cloudevents+json; charset=utf-8
```

MQTT 3.1.1 transporta exactamente el mismo JSON, pero el Content Type queda implícito porque esa versión del protocolo no dispone de la propiedad MQTT correspondiente.

Ejemplo del payload MQTT generado:

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
  "causationid": "command-2461",
  "data": {
    "sensorId": "sensor-42",
    "temperature": 23.7,
    "humidity": 41.2,
    "timestamp": "2026-08-25T08:57:24.5489680Z"
  }
}
```

La fábrica construye y valida los atributos obligatorios:

- `specversion`, siempre `1.0`;
- `id`, generado automáticamente si no se proporciona;
- `source` y `type`, declarados en `CloudEventDescriptor`;
- `datacontenttype`, `application/json` de forma predeterminada;
- `time`, usando la fecha de ocurrencia indicada o UTC actual;
- `dataschema` para contratos gobernados;
- `subject` para una entidad funcional concreta.

Las extensiones comunes disponibles son `correlationid`, `causationid`, `traceparent`, `tracestate`, `negotiationid` y `expiresat`. `traceparent` y `tracestate` se obtienen automáticamente de `Activity.Current` cuando no se indican explícitamente. Los nombres de extensiones adicionales deben usar únicamente minúsculas ASCII y dígitos.

Los atributos CloudEvents se escriben en el nivel superior y no se duplican dentro de `data`. Durante el consumo se validan antes de deserializar el dato funcional. Un payload desnudo, una versión diferente de `1.0` o un Content Type MQTT 5 diferente se rechazan.

La identidad idempotente es siempre el par:

```text
source + id
```

Puede obtenerse mediante `message.Identity`. Nunca debe utilizarse `id` de forma aislada como clave global.

## 6. Compatibilidad reactiva

`TopicSet<T>` continúa implementando `IObservable<T>`:

```csharp
using System.Reactive.Linq;

using var subscription = context.SensorReadings
    .Where(reading => reading.Temperature > 25)
    .Subscribe(reading =>
        Console.WriteLine($"Alerta: {reading.Temperature} °C"));
```

La suscripción se cancela al liberar el `IDisposable`. Para lógica asíncrona, control de backpressure y acknowledgement explícito debe preferirse `ReadAllAsync`.

## 7. Ciclo de vida y readiness

`IMqttBus.State` recorre los siguientes estados:

```text
Created
  -> Connecting
  -> Connected
  -> Subscribing
  -> Ready
  -> Reconnecting
  -> Draining
  -> Stopped
```

`Faulted` indica que la conexión inicial falló o que se agotó la política de reconexión.

```csharp
bus.StateChanged += (_, change) =>
{
    Console.WriteLine(
        $"MQTT: {change.Previous} -> {change.Current}");
};

if (bus.IsReady)
{
    // El Worker tiene conexión y sus suscripciones están restauradas.
}
```

La readiness se retira al abandonar `Ready`. `WasSessionRestored` indica si el broker devolvió una sesión existente en el último CONNACK.

Después de una reconexión:

- Si el broker restauró la sesión, no se duplican las suscripciones.
- Si no la restauró, `MqttNetBus` vuelve a suscribir todos los filtros activos.

## 8. Last Will and Testament

```csharp
options.LastWill.MessageExpiry = TimeSpan.FromMinutes(5);
options.LastWill.UseServiceUnavailableCloudEvent(
    "services/equipment-worker/availability");
```

Ante una desconexión abrupta, el broker publica un CloudEvent con estado `UNAVAILABLE`. De forma predeterminada el mensaje es retained. En un cierre normal, la biblioteca publica explícitamente el mismo estado antes de desconectarse.

Si no se especifica un topic, se utiliza:

```text
services/{ClientId}/availability
```

## 9. Pruebas sin broker

`InMemoryMqttBus` implementa el mismo contrato que `MqttNetBus`:

```csharp
await using var bus = new InMemoryMqttBus();
var model = new TopicModelBuilder()
    .Add<SensorReading>(
        nameof(ApplicationMqttContext.SensorReadings),
        "tests/sensors/events")
    .Build();

var context = new ApplicationMqttContext(bus, model);
using var cancellation = new CancellationTokenSource();

await using var reader = context.SensorReadings.ReadAllAsync(
    SubscriptionOptions.Default,
    cancellation.Token).GetAsyncEnumerator(cancellation.Token);

// MoveNextAsync inicia y registra la suscripción antes de publicar.
var nextMessage = reader.MoveNextAsync().AsTask();

await context.SensorReadings.PublishAsync(
    new SensorReading
    {
        SensorId = "test-sensor",
        Temperature = 20
    },
    CloudEventPublishOptions.Default,
    cancellation.Token);

if (!await nextMessage)
    throw new InvalidOperationException("La suscripción terminó sin recibir datos.");

await reader.Current.AcknowledgeAsync(cancellation.Token);
var received = reader.Current.Data;
```

El bus en memoria respeta filtros con comodines y canales acotados, por lo que permite probar el flujo tipado y la cancelación sin iniciar Mosquitto.

## 10. Uso directo sin Generic Host

Aunque Generic Host es la opción recomendada, también puede controlarse el transporte manualmente:

```csharp
var options = new MqttReactiveOrmOptions
{
    ClientId = "standalone-client",
    Server = "localhost",
    Port = 1883
};

await using var bus = new MqttNetBus(options);
await bus.ConnectAsync(cancellationToken);

// Publicación y consumo...

await bus.DisconnectAsync(cancellationToken);
```

## Buenas prácticas

- Utilizar un `ClientId` estable y único por instancia de servicio.
- Propagar siempre el `CancellationToken` del Host.
- Confirmar el mensaje solamente después de completar el efecto aplicativo.
- Mantener el procesamiento idempotente: MQTT puede entregar duplicados.
- Elegir una capacidad de suscripción coherente con la carga y memoria disponibles.
- No usar retained para flujos de eventos; reservarlo para estados o snapshots.
- No bloquear tareas asíncronas mediante `.Wait()` o `.GetAwaiter().GetResult()`.
- Supervisar `IsReady` y `StateChanged` para health checks y observabilidad.

## Demo

La carpeta [Demo](Demo) contiene un Worker completo con:

- configuración MQTT 5;
- sesión persistente;
- reconexión exponencial;
- LWT CloudEvent;
- contexto y modelo registrados mediante DI;
- consumo cancelable con backpressure;
- acknowledgement posterior al procesamiento;
- publicación de un mensaje de ejemplo.

Ejecutar:

```bash
dotnet run --project Demo/Demo.csproj
```

Detener con `Ctrl+C` para comprobar el cierre ordenado.

## Seguridad TLS y mTLS

La configuración mTLS aplica validación estricta. La cadena del certificado del broker debe ser confiable para el sistema operativo, el DNS/SAN debe coincidir con `ExpectedServerName`, el certificado debe estar vigente y la comprobación de revocación permanece activa.

```csharp
using Net.Mqtt.ReactiveOrm.Security;

var certificateProvider = new PfxCertificateProvider(
    "/run/secrets/equipment-worker.pfx",
    Environment.GetEnvironmentVariable("MQTT_CERTIFICATE_PASSWORD"));

builder.Services.AddMqttReactiveOrm(options =>
{
    options.ProtocolVersion = MqttProtocolVersion.V500;
    options.Server = "mosquitto.enterprise.svc.internal";
    options.Port = 8883;
    options.ClientId = "enterprise-equipment-worker";

    options.Security.UseMutualTls(mtls =>
    {
        mtls.ClientCertificateProvider = certificateProvider;
        mtls.RequireTrustedServerCertificate = true;
        mtls.CheckCertificateRevocation = true;
        mtls.ExpectedServerName = "mosquitto.enterprise.svc.internal";
        mtls.ExpectedClientIdentity = "enterprise-equipment-worker";
        mtls.ExpirationWarningThreshold = TimeSpan.FromDays(30);
        mtls.ExpirationCheckInterval = TimeSpan.FromHours(1);
    });
});
```

`ExpectedClientIdentity` vincula la identidad configurada del módulo con el DNS SAN o CN del certificado cliente. El certificado debe contener una clave privada y encontrarse dentro de su periodo de validez.

Las opciones que desactivan la confianza o la revocación son rechazadas durante la validación:

```csharp
mtls.RequireTrustedServerCertificate = false; // Prohibido.
mtls.CheckCertificateRevocation = false;      // Prohibido.
```

La biblioteca nunca habilita los equivalentes de:

```text
AllowUntrustedCertificates = true
IgnoreCertificateChainErrors = true
IgnoreCertificateRevocationErrors = true
```

### Proveedores de certificados

Desde un archivo PFX:

```csharp
mtls.ClientCertificateProvider = new PfxCertificateProvider(
    "/run/secrets/client.pfx",
    password);
```

Desde archivos PEM:

```csharp
mtls.ClientCertificateProvider = new PemCertificateProvider(
    "/run/secrets/client.crt",
    "/run/secrets/client.key");
```

Desde el almacén de certificados. La clave privada se utiliza directamente y no necesita ser exportable:

```csharp
mtls.ClientCertificateProvider = new StoreCertificateProvider(
    thumbprint,
    StoreName.My,
    StoreLocation.CurrentUser);
```

Desde un gestor de secretos o HSM mediante un resolver asíncrono:

```csharp
var provider = new SecretCertificateProvider(async cancellationToken =>
    await secretStore.GetCertificateAsync("mqtt-client", cancellationToken));

mtls.ClientCertificateProvider = provider;
```

Los proveedores PFX y PEM vigilan los archivos. Cuando Kubernetes, Docker Secrets u otro agente reemplaza el certificado, el bus recarga las credenciales y fuerza una reconexión ordenada. Para un proveedor de secretos, la integración debe señalar la rotación:

```csharp
provider.SignalRotation();
```

La expiración puede supervisarse desde el bus:

```csharp
bus.CertificateExpiring += (_, certificate) =>
{
    logger.LogWarning(
        "El certificado {Thumbprint} expira en {Remaining}",
        certificate.Thumbprint,
        certificate.Remaining);
};
```

Durante una rotación, `IsReady` deja de ser verdadero, la conexión pasa por `Draining` y solo vuelve a `Ready` después de autenticar el nuevo certificado y restaurar las suscripciones.

## Alcance de esta versión

Esta versión implementa transporte inyectable, API asíncrona, ciclo de vida MQTT, seguridad TLS/mTLS y CloudEvents 1.0 tipados para todos los mensajes. La validación avanzada de contratos y esquemas, inbox/outbox, retry/DLQ, protocolo de capacidades y OpenTelemetry forman parte de evoluciones posteriores.
