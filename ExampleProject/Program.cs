using ExampleProject.Topics;

namespace ExampleProject
{
    internal class Program
    {
        private static MqttContext _context;
        public static async Task Main(string[] args)
        {
            _context = new MqttContext();

            //Suscribirse a mensajes
            await _context.Sensor_hex_001.SubscribeAsync(async message =>
                {
                    Console.WriteLine($"Mensaje recibido: {message}");
                    await Task.CompletedTask;
                }
            );

            await _context.Sensor_Temp_001.SubscribeAsync(async message =>
                {
                    Console.WriteLine($"Mensaje recibido: {message.Temperature}");
                    await Task.CompletedTask;
                }
            );

            await _context.Sensor_Temp_001.PublishAsync(new Sensor_Temp_001 
            { 
                Temperature = 59, 
                Humidity = 60 
            });

            await _context.Sensor_hex_001.PublishAsync(2.2);
            Console.ReadLine();
        }
    }
}
