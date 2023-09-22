using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class ConnectionMessage : INetworkMessage
    {
        public string SenderClientId { get; set; }
        public string SenderConnectionId { get; set; }

        /// <summary>
        /// This empty constructor shouldn't be called but must exist so
        /// System.Text.Json can properly serialize/deserialize this object!
        /// </summary>
        public ConnectionMessage() { }

        public ConnectionMessage(string senderClientId, string senderConnectionId)
        {
            SenderClientId = senderClientId;
            SenderConnectionId = senderConnectionId;
        }
    }
}
