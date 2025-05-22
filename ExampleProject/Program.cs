using ExampleProject.Topics;

namespace ExampleProject
{
    internal class Program
    {
        private static MqttContext _context;
        public static async Task Main(string[] args)
        {
            _context = new MqttContext();

            var Sensor_Temp_001 = new Sensor_Temp_001
            {
                Status = "Online",
            };

            // Suscribirse a mensajes
            await _context.Sensor_Temp_001.SubscribeAsync(
                async message =>
                {
                    Console.WriteLine($"Mensaje recibido: {message.Status} del dispositivo {message.Id}");
                }
            );

            // Publish a message
            await _context.Sensor_Temp_001.PublishAsync(Sensor_Temp_001);

            Console.WriteLine($"Published message: {Sensor_Temp_001.Status} from device {Sensor_Temp_001.Id}");

            // Keep the application running to receive messages
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
