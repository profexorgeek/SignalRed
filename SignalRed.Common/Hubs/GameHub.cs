using Microsoft.AspNetCore.SignalR;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;

namespace SignalRed.Common.Hubs
{
    public class GameHub : Hub<IGameClient>
    {
        static List<ChatMessage> messages = new List<ChatMessage>();
        static Dictionary<string, string> users = new Dictionary<string, string>();

        public async Task RegisterUser(string username)
        {
            users.Add(Context.ConnectionId, username);
            Console.WriteLine($"{username} has joined the server...");
            await Clients.All.RegisterUser(username);
        }

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
