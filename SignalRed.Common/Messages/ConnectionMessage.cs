using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class ConnectionMessage : INetworkMessage
    {
        public string SenderId { get; set; }
        public string SenderConnectionId { get; set; }

        public ConnectionMessage(string senderId, string senderConnectionId)
        {
            SenderId = senderId;
            SenderConnectionId = senderConnectionId;
        }
    }
}
