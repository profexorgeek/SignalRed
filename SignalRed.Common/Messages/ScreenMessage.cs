using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class ScreenMessage : INetworkMessage
    {
        public string? SenderId { get; set; }
        public string? SenderConnectionId { get; set; }

        /// <summary>
        /// The target screen that all clients should transition to
        /// </summary>
        public string TargetScreen { get; set; }

        public ScreenMessage(string targetScreen)
        {
            TargetScreen = targetScreen;
        }
    }
}
