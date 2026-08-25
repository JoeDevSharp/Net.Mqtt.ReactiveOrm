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

El codec predeterminado serializa los datos como JSON UTF-8. Puede sustituirse inyectando una implementación de `IMqttCodec` en el contexto.

## 2. Registrar explícitamente el modelo de topics

El contexto no inspecciona ni modifica propiedades derivadas durante su construcción. Cada `TopicSet<T>` se resuelve desde un modelo explícito e inmutable:

```csharp
using Net.Mqtt.ReactiveOrm;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Models;

public sealed class ApplicationMqttContext(
    IMqttBus bus,
    ITopicModel model) : MqttOrmContext(bus, model)
{
    public TopicSet<SensorReading> SensorReadings => Set<SensorReading>();

    public static TopicModel CreateModel() => new TopicModelBuilder()
        .Add<SensorReading>(
            nameof(SensorReadings),
            "factory/sensors/SensorReading/events",
            QoSLevel.AtLeastOnce)
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
    CloudEventPublishOptions.Default,
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

## Alcance de esta versión

Esta versión implementa las extensiones de transporte inyectable, API asíncrona y ciclo de vida MQTT. Las capacidades de CloudEvents para todos los mensajes de aplicación, contratos y esquemas, inbox/outbox, retry/DLQ, protocolo de capacidades y OpenTelemetry forman parte de evoluciones posteriores.
