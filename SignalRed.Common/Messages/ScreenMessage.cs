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

        public ScreenMessage(string senderClientId, string connectionId, string targetScreen)
        {
            SenderClientId = senderClientId;
            SenderConnectionId = connectionId;
            TargetScreen = targetScreen;
        }
    }
}
