using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class ScreenMessage : NetworkMessage
    {

        /// <summary>
        /// The target screen that all clients should transition to
        /// </summary>
        public string TargetScreen { get; set; }

        /// <summary>
        /// This empty constructor shouldn't be called but must exist so
        /// System.Text.Json can properly serialize/deserialize this object!
        /// </summary>
        public ScreenMessage() { }

        public ScreenMessage(string senderClientId, string senderConnectionId, double sendTime, string targetScreen)
            :base(senderClientId, senderConnectionId, sendTime)
        {
            TargetScreen = targetScreen;
        }
    }
}
