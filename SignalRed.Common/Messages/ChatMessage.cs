namespace SignalRed.Common.Messages
{
    public interface IGameClient
    {
        Task ReceiveMessage(ChatMessage message);
        Task ReceiveAllMessages(List<ChatMessage> message);
    }

    public class ChatMessage
    {
        public string ClientId { get; set; }
        public DateTime Time { get; set; }
        public string Message { get; set; } = "";

        public ChatMessage() { }

        public ChatMessage(string? id, string message)
        {
            ClientId = id;
            Message = message;
            Time = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"{ClientId} ({Time}): {Message}";
        }
    }
}
