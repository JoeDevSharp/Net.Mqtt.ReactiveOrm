using ExampleProject.Topics;

namespace ExampleProject
{
    internal class Program
    {
        private static MqttContext _context;
        public static async Task Main(string[] args)
        {
            _context = new MqttContext();

            var deviceStatusMessage = new DeviceStatusMessage
            {
                Status = "Online",
            };

            // Suscribirse a mensajes
            await _context.DeviceStatusMessage.SubscribeAsync(
                async message =>
                {
                    Console.WriteLine($"Mensaje recibido: {message.Status} del dispositivo {message.DeviceId}");
                },
                new { DeviceId = "iPhone_5587" }
            );

            // Publish a message
            await _context.DeviceStatusMessage.PublishAsync(deviceStatusMessage
                , new { DeviceId = "iPhone_5587" });

            Console.WriteLine($"Published message: {deviceStatusMessage.Status} from device {deviceStatusMessage.DeviceId}");

            // Keep the application running to receive messages
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
