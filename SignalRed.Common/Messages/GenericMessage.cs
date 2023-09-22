using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class GenericMessage : INetworkMessage
    {
        public string SenderClientId { get; set; }
        public string SenderConnectionId { get; set; }

        public string MessageKey { get; set; }
        public string MessageValue { get; set; }

        /// <summary>
        /// This empty constructor shouldn't be called but must exist so
        /// System.Text.Json can properly serialize/deserialize this object!
        /// </summary>
        public GenericMessage() { }

        public GenericMessage(string senderClientId, string senderConnectionId, string messageKey, string messageValue)
        {
            SenderClientId = senderClientId;
            SenderConnectionId = senderConnectionId;
            MessageKey = messageKey;
            MessageValue = messageValue;
        }
    }
}
