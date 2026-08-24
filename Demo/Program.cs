
using System.Reactive.Linq;
using MQTTnet;
using Net.Mqtt.ReactiveOrm.Bus;

namespace Demo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Crear una nueva instancia del contexto MQTT, que gestiona la conexión y los temas MQTT
            var options = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).Build();
            await using var bus = new MqttNetBus(options);
            var context = new MqttContext(bus, MqttContext.CreateModel());

            // Suscribirse al tema DHT230222_Modules y filtrar los mensajes donde la temperatura es mayor a 20.5°C
            // Solo los módulos que cumplen esta condición activarán la acción definida en Subscribe
            context.DHT230222_Modules
                .Where(module => module.Temperature > 20.5)
                .Subscribe(module =>
                {
                    // Imprimir una alerta en consola cuando la temperatura supera el umbral
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Alert: Temperature: {module.Temperature}, Humidity: {module.Humidity}, Timestamp: {module.Timestamp}");
                    Console.ResetColor();
                });

            // Publicar un mensaje de ejemplo en el tema DHT230222_Modules
            // Se envía un objeto con temperatura 18°C y humedad 45%
            await context.DHT230222_Modules.PublishAsync(new()
            {
                Temperature = 30,
                Humidity = 45.0,
            });

            // Mantener la aplicación en ejecución hasta que el usuario presione una tecla
            Console.Read();
        }
    }
}
