using ExampleProject.Topics;
using System.Reactive.Linq;

namespace ExampleProject
{
    internal class Program
    {
        private static MqttContext _context;
        public static void Main(string[] args)
        {
            _context = new MqttContext();

            _context.Sensor_Temp_001
                 .Observable()
                 .Where(x => x.Temperature > 50)
                 .Subscribe(x =>
                 {
                     Console.WriteLine(x.Temperature);
                     _context.Sensor_Temp_001.Unsubscribe();
                 } );
            
            _context.Sensor_Temp_001.PublishAsync(new Sensor_Temp_001
            {
                Temperature = 59,
                Humidity = 60
            });

            _context.Sensor_Temp_001.PublishAsync(new Sensor_Temp_001
            {
                Temperature = 5,
                Humidity = 60
            });

            Console.ReadLine();
        }
    }
}
