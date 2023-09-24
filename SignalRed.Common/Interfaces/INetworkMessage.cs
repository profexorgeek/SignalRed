namespace SignalRed.Common.Interfaces
{
    public interface INetworkMessage
    {
        /// <summary>
        /// The unique identifier of the sender, persistent
        /// across connection/disconnection events.
        /// </summary>
        public string SenderClientId { get; }

        /// <summary>
        /// The connection identifier used by the server to 
        /// identify connected clients. Does not persist across
        /// connection/disconnection events.
        /// </summary>
        public string SenderConnectionId { get; }

        /// <summary>
        /// The time this message originated, used by game clients to apply predictive
        /// physics and interpolation.
        /// </summary>
        public double SendTime { get; }

        /// <summary>
        /// This method provides a way to get a time delta between when a message was sent
        /// and the current time, in seconds, which is the increment
        /// most game engines use for concepts like velocity, in a value that is guaranteed to
        /// be greater or equal to zero and a number. This prevents weird interpolation behavior
        /// due to small timing inconsistencies.
        /// </summary>
        public float DeltaSeconds { get; }
    }
}