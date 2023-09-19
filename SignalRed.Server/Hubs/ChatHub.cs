using Microsoft.AspNetCore.SignalR;
using SignalRed.Common.Messages;

namespace SignalRed.Server.Hubs
{
    public class GameHub : Hub<IGameClient>
    {
        static List<ChatMessage> messages = new List<ChatMessage>();

        public async Task ReceiveMessage(ChatMessage message)
        {
            messages.Add(message);
            Console.WriteLine("Received message: " + message);
            await Clients.All.ReceiveMessage(message);
        }

        public async Task ReceiveAllMessages()
        {
            Console.WriteLine("Received request for all messages");
            await Clients.Caller.ReceiveAllMessages(messages);
        }
    }
}
