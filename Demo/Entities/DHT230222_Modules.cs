namespace Demo.Entities
{
    /// <summary>Represents a temperature and humidity reading produced by the demo sensor.</summary>
    public class DHT230222_Modules
    {
        /// <summary>Gets or sets the measured temperature.</summary>
        public double Temperature { get; set; }
        /// <summary>Gets or sets the measured relative humidity.</summary>
        public double Humidity { get; set; }
        /// <summary>Gets or sets the UTC time at which the reading occurred.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
