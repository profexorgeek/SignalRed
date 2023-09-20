namespace SignalRed.Common.Messages
{
    public class UserMessage
    {
        public string ClientId { get; set; }
        public string UserName { get; set; }

        public UserMessage(string clientId, string userName)
        {
            ClientId = clientId;
            UserName = userName;
        }
    }
}
