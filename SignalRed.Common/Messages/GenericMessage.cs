using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class GenericMessage : NetworkMessage
    {

        public string MessageKey { get; set; }
        public string MessageValue { get; set; }

        /// <summary>
        /// This empty constructor shouldn't be called but must exist so
        /// System.Text.Json can properly serialize/deserialize this object!
        /// </summary>
        public GenericMessage() { }

        public GenericMessage(string senderClientId, string senderConnectionId, double sendTime, string messageKey, string messageValue)
            :base(senderClientId, senderConnectionId, sendTime)
        {
            MessageKey = messageKey;
            MessageValue = messageValue;
        }
    }
}
