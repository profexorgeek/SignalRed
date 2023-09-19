using SignalRed.Common.Messages;

namespace SignalRed.Common.Interfaces
{
    public interface IGameClient
    {
        Task RegisterUser(string username);
        Task ReceiveMessage(ChatMessage message);
        Task ReceiveAllMessages(List<ChatMessage> message);
    }
}
