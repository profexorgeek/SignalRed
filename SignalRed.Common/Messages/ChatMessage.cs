namespace SignalRed.Common.Messages
{
    public class ChatMessage
    {
        public string SenderId { get; set; }
        public string UserName { get; set; }
        public DateTime Time { get; set; }
        public string Message { get; set; } = "";

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
