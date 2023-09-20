using Microsoft.AspNetCore.SignalR;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Linq;
using System.Linq.Expressions;

namespace SignalRed.Common.Hubs
{
    public class GameHub : Hub<IGameClient>
    {
        static List<ChatMessage> messages = new List<ChatMessage>();
        static List<EntityMessage> entities = new List<EntityMessage>();
        static List<UserMessage> users = new List<UserMessage>();
        static ScreenMessage currentScreen = new ScreenMessage();

        public async Task MoveToScreen(ScreenMessage message)
        {
            Console.WriteLine($"Received request to move to screen: {message.NewScreen}");
            currentScreen = message;
            await Clients.All.MoveToScreen(message);
        }
        public async Task RequestCurrentScreen()
        {
            await Clients.Caller.MoveToScreen(currentScreen);
        }

        public async Task UpdateUser(UserMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.ClientId)) return;
            var existing = users.Where(u => u.ClientId == message.ClientId).FirstOrDefault();
            if (existing != null)
            {
                var oldName = existing.UserName;
                existing.UserName = message.UserName;
                Console.WriteLine($"{oldName} has changed their name to {message.UserName}");
            }
            else
            {
                users.Add(message);
                Console.WriteLine($"{message.UserName} with id {message.ClientId} has joined the server!");
            }            
            await Clients.All.RegisterUser(message);
        }
        public async Task ReckonUsers()
        {
            CleanUserList();
            await Clients.Caller.ReckonUsers(users);
        }
        public async Task DeleteUser(UserMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.ClientId)) return;

            var existing = users.Where(u => u.ClientId == message.ClientId).FirstOrDefault();
            if (existing == null)
            {
                Console.WriteLine($"Attempted to remove {message.UserName} but they don't exist on the server!");
            }
            else
            {
                Console.WriteLine($"{message.UserName} has left the server.");
                users.Remove(existing);

                // TODO: force disconnect?

                await Clients.All.DeleteUser(message);
            }
        }

        public async Task SendChat(ChatMessage message)
        {
            // TODO: add targeted messages (DMs)
            messages.Add(message);
            Console.WriteLine("Message: " + message);
            await Clients.All.ReceiveChat(message);
        }
        public async Task RequestAllChats()
        {
            foreach(var msg in messages)
            {
                await Clients.Caller.ReceiveChat(msg);
            }
        }
        public async Task DeleteAllChats()
        {
            messages.Clear();
            await Clients.All.DeleteAllChats();
        }


        public async Task CreateEntity(EntityMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Id)) return;
            var existing = entities.Where(e => e.Id == message.Id).FirstOrDefault();
            if(existing == null)
            {
                entities.Add(message);
                await Clients.All.CreateEntity(message);
            }
        }
        public async Task UpdateEntity(EntityMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Id)) return;
            var existing = entities.Where(e => e.Id == message.Id).FirstOrDefault();
            if(existing != null)
            {
                entities.Remove(existing);
                entities.Add(message);

                await Clients.All.UpdateEntity(message);
            }
        }
        public async Task DeleteEntity(EntityMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Id)) return;
            var existing = entities.Where(e => e.Id == message.Id).FirstOrDefault();
            if(existing != null)
            {
                entities.Remove(existing);
                await Clients.All.DeleteEntity(message);
            }
        }
        public async Task ReckonAllEntities()
        {
            CleanUserList();
            CleanEntityList();

            await Clients.Caller.ReckonEntities(entities);
        }

        /// <summary>
        /// Cleans the user list, removing any users that aren't found
        /// in the connected clients
        /// </summary>
        void CleanUserList()
        {
            for (var i = users.Count - 1; i > -1; i--)
            {
                if (Clients.Client(users[i].ClientId) == null)
                {
                    users.RemoveAt(i);
                }
            }
        }
        /// <summary>
        /// Removes any entities that don't have owners, purging entities owned
        /// by disconnected users.
        /// </summary>
        void CleanEntityList()
        {
            for(var i = entities.Count - 1; i > -1; i--)
            {
                var entity = entities[i];
                if(users.Any(u => u.ClientId == entity.Owner) == false)
                {
                    entities.RemoveAt(i);
                }
            }
        }
    }
}
