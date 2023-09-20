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
        public async Task RequestAllUsers()
        {
            for(var i = 0; i < users.Count; i++)
            {
                await Clients.Caller.RegisterUser(users[i]);
            }
        }
        public async Task DeleteUser(UserMessage message)
        {
            var existing = users.Where(u => u.ClientId == message.ClientId).FirstOrDefault();
            if(existing == null)
            {
                throw new Exception($"Attempted to remove {message.UserName} but they don't exist on the server!");
            }
            Console.WriteLine($"{message.UserName} has left the server.");
            users.Remove(existing);
        }


        public async Task SendChat(ChatMessage message)
        {
            // TODO: add targeted messages (DMs)
            messages.Add(message);
            Console.WriteLine("Message: " + message);
            await Clients.All.ReceiveMessage(message);
        }
        public async Task RequestAllMessages()
        {
            foreach(var msg in messages)
            {
                await Clients.Caller.ReceiveMessage(msg);
            }
        }


        public async Task UpdateEntity(EntityMessage message)
        {
            var existing = entities.Where(e => e.EntityId == message.EntityId).FirstOrDefault();
            switch(message.UpdateType)
            {
                case UpdateType.Unknown:
                    // NOOP
                    break;
                case UpdateType.Create:
                    if(existing == null)
                    {
                        entities.Add(message);
                    }
                    else
                    {
                        throw new Exception($"Attempted to create entity {message.EntityId} whose ID already exists");
                    }
                    break;
                case UpdateType.Update:
                    if(existing == null)
                    {
                        throw new Exception($"Attempted to update entity {message.EntityId} who does not exist on the server.");
                    }
                    else
                    {
                        entities.Remove(existing);
                        entities.Add(message);
                    }
                    break;
                case UpdateType.Delete:
                    if(existing == null)
                    {
                        throw new Exception($"Attempted to delete entity {message.EntityId} which does not exist on the server.");
                    }
                    else
                    {
                        entities.Remove(existing);
                    }
                    break;
            }

            await Clients.All.UpdateEntity(message);
        }
        public async Task RequestAllEntities()
        {
            foreach(var entity in entities)
            {
                await Clients.Caller.UpdateEntity(entity);
            }
        }
    }
}
