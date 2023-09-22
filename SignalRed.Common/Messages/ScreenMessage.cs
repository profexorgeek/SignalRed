using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class ScreenMessage : INetworkMessage
    {
        public string? SenderClientId { get; set; }
        public string? SenderConnectionId { get; set; }

        /// <summary>
        /// The target screen that all clients should transition to
        /// </summary>
        public string TargetScreen { get; set; }

        /// <summary>
        /// This empty constructor shouldn't be called but must exist so
        /// System.Text.Json can properly serialize/deserialize this object!
        /// </summary>
        public ScreenMessage() { }

        public ScreenMessage(string senderClientId, string connectionId, string targetScreen)
        {
            SenderClientId = senderClientId;
            SenderConnectionId = connectionId;
            TargetScreen = targetScreen;
        }
    }
}
