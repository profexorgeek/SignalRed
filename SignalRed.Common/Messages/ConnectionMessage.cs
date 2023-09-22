using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class ConnectionMessage : INetworkMessage
    {
        public string SenderClientId { get; set; }
        public string SenderConnectionId { get; set; }

        public ConnectionMessage(string senderClientId, string senderConnectionId)
        {
            SenderClientId = senderClientId;
            SenderConnectionId = senderConnectionId;
        }
    }
}
