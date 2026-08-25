namespace Net.Mqtt.Infrastructure.Enums
{
    /// <summary>Defines qo slevel.</summary>
    public enum QoSLevel
    {
        /// <summary>Gets at most once.</summary>
        AtMostOnce = 0x00,
        /// <summary>Gets at least once.</summary>
        AtLeastOnce = 0x01,
        /// <summary>Gets exactly once.</summary>
        ExactlyOnce = 0x02
    }
}
