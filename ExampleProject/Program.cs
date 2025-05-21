using ExampleProject.Models;

namespace ExampleProject
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var context = new MqttContext();

            var deviceStatusMessage = new DeviceStatusMessage
            {
                Status = "Online",
            };

            // Suscribirse a mensajes
            await context.DeviceStatusMessage.SubscribeAsync(
                async message =>
                {
                    await Task.Delay(2);
                    Console.WriteLine($"Mensaje recibido: {message.Status} del dispositivo {message.DeviceId}");
                },
                new { DeviceId = "iPhone_5587" }
            );

            // Publish a message
            await context.DeviceStatusMessage.PublishAsync(deviceStatusMessage
                , new { DeviceId = "iPhone_5587" });

            Console.WriteLine($"Published message: {deviceStatusMessage.Status} from device {deviceStatusMessage.DeviceId}");

            // Keep the application running to receive messages
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
