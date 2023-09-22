namespace SignalRed.Common.Interfaces
{
    public interface INetworkMessage
    {
        /// <summary>
        /// The unique identifier of the sender, persistent
        /// across connection/disconnection events.
        /// </summary>
        public string SenderId { get; }

        /// <summary>
        /// The connection identifier used by the server to 
        /// identify connected clients. Does not persist across
        /// connection/disconnection events.
        /// </summary>
        public string SenderConnectionId { get; }
    }
}