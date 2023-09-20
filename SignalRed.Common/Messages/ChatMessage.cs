namespace SignalRed.Common.Messages
{
    public class ChatMessage
    {
        public string SenderId { get; set; } = "Unknown";
        public string UserName { get; set; } = "Unknown";
        public DateTime Time { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = "Unknown";

        public ChatMessage() { }

        public ChatMessage(string? id, string user, string message)
        {
            SenderId = id;
            UserName = user;
            Message = message;
            Time = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"{UserName} ({Time.ToLocalTime()}): {Message}";
        }
    }
}
