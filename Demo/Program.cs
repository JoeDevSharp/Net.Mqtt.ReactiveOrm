// Importa el espacio de nombres donde se encuentra el Worker de la aplicación.
using Demo;
// Importa el contexto MQTT tipado definido específicamente para esta demo.
using Demo.Mqtt;
// Habilita el registro y la resolución de servicios mediante inyección de dependencias.
using Microsoft.Extensions.DependencyInjection;
// Proporciona el Generic Host que controla el inicio, la ejecución y el cierre del Worker.
using Microsoft.Extensions.Hosting;
// Contiene la enumeración utilizada para seleccionar la versión del protocolo MQTT.
using MQTTnet.Formatter;
// Importa las opciones y contratos de configuración de Net.Mqtt.ReactiveOrm.
using Net.Mqtt.ReactiveOrm.Models;

// Crea el constructor del Host usando los argumentos recibidos por la aplicación.
// El Host también configura logging, configuración y gestión de señales como Ctrl+C.
var builder = Host.CreateApplicationBuilder(args);

// Registra una única conexión MQTT compartida y su servicio de ciclo de vida en el contenedor DI.
builder.Services.AddMqttReactiveOrm(options =>
{
    // Selecciona MQTT 5 para disponer de sesiones con expiración, LWT avanzado y límites negociados.
    options.ProtocolVersion = MqttProtocolVersion.V500;
    // Indica el nombre DNS o la dirección IP del broker Mosquitto.
    options.Server = "localhost";
    // Utiliza el puerto MQTT TCP estándar expuesto por el docker-compose de la solución.
    options.Port = 1883;
    // Define una identidad estable para que el broker pueda recuperar la sesión de este Worker.
    options.ClientId = "reactive-orm-demo-worker";
    // Solicita tráfico keep-alive cada 30 segundos para detectar conexiones interrumpidas.
    options.KeepAlive = TimeSpan.FromSeconds(30);
    // Limita a 10 segundos la espera de las operaciones de conexión con el broker.
    options.Timeout = TimeSpan.FromSeconds(10);
    // Rechaza paquetes MQTT superiores a un MiB para limitar el consumo de memoria.
    options.MaximumPacketSize = 1024 * 1024;
    // Permite como máximo 32 mensajes QoS 1 o QoS 2 pendientes de confirmación simultáneamente.
    options.ReceiveMaximum = 32;

    // Conserva la sesión anterior en vez de solicitar una sesión MQTT completamente nueva.
    options.Session.CleanStart = false;
    // Pide al broker que conserve la sesión y sus suscripciones durante un máximo de 24 horas.
    options.Session.Expiry = TimeSpan.FromHours(24);
    // Activa la reconexión automática mediante espera exponencial con jitter.
    options.Reconnect.UseExponentialBackoff(
        // Realiza el primer reintento aproximadamente un segundo después de la desconexión.
        initialDelay: TimeSpan.FromSeconds(1),
        // Impide que la espera entre reintentos crezca por encima de 30 segundos.
        maximumDelay: TimeSpan.FromSeconds(30));
    // Hace que el estado de indisponibilidad publicado por el LWT expire tras cinco minutos.
    options.LastWill.MessageExpiry = TimeSpan.FromMinutes(5);
    // Configura un Last Will CloudEvent UNAVAILABLE que Mosquitto publicará si el Worker cae abruptamente.
    options.LastWill.UseServiceUnavailableCloudEvent();
// Finaliza la configuración y el registro de Net.Mqtt.ReactiveOrm.
});

// Registra el mapa inmutable que relaciona cada TopicSet tipado con su topic MQTT.
builder.Services.AddSingleton<ITopicModel>(_ => MqttContext.CreateModel());
// Registra un único MqttContext que reutiliza la conexión MQTT compartida durante toda la ejecución.
builder.Services.AddSingleton<MqttContext>();
// Registra SensorWorker para que el Host inicie y detenga automáticamente su trabajo asíncrono.
builder.Services.AddHostedService<SensorWorker>();

// Construye el Host, inicia sus servicios y espera de forma asíncrona hasta recibir la orden de parada.
await builder.Build().RunAsync();
