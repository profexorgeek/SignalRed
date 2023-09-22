using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class GenericMessage : INetworkMessage
    {
        public string SenderClientId { get; set; }
        public string SenderConnectionId { get; set; }

        public string? MessageKey { get; set; }
        public string? MessageValue { get; set; }

        public GenericMessage(string senderId, string senderConnectionId, string messageKey, string messageValue)
        {
            SenderClientId = senderId;
            SenderConnectionId = senderConnectionId;
        }
    }
}
