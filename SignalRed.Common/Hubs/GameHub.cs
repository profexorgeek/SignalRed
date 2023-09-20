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


        public async Task CreateOrUpdateUser(UserMessage message)
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

        }




        /// <summary>
        /// Notifies all clients that they should update the 
        /// provided entity.
        /// </summary>
        /// <typeparam name="T">The type of entity to update</typeparam>
        /// <param name="entityToUpdate">The entity to update</param>
        public async Task UpdateEntity(EntityMessage message)
        {
            Type targetType = Type.GetType(typeString) ?? typeof(INetworkEntityState);
            var rawJson = entityElement.GetRawText();
            var entityToUpdate = JsonSerializer.Deserialize(rawJson, targetType);
            var entityAsGeneric = (INetworkEntityState)entityToUpdate;

            if(entityAsGeneric == null)
            {
                throw new Exception($"Failed to deserialize entity of type {typeString}, update message not implement INetworkEntityState");
            }

            string id = entityAsGeneric.EntityId;
            bool success = false;
            switch(entityAsGeneric.UpdateType)
            {
                case UpdateType.Unknown:
                    // NOOP, shouldn't be here!
                    break;
                case UpdateType.Create:
                    if (entities.ContainsKey(id))
                    {
                        throw new Exception("Attempted to create an entity with an ID that already exists!");
                    }
                    entities.Add(id, entityAsGeneric);
                    success = true;
                    break;
                case UpdateType.Update:
                    if (entities.ContainsKey(id))
                    {
                        entities[id] = entityAsGeneric;
                    }
                    success = true;
                    break;
                case UpdateType.Delete:
                    if(entities.ContainsKey(id))
                    {
                        entities.Remove(id);
                        success = true;
                    }
                    break;
            }

            if(success)
            {
                await Clients.All.UpdateEntity(typeString, entityToUpdate);
            }
        }

        /// <summary>
        /// Provides the caller with all entities known to the server
        /// </summary>
        public async Task ReceiveAllEntities()
        {
            Console.WriteLine("Received request for all entities");
            foreach(var kvp in  entities)
            {
                await Clients.Caller.UpdateEntity(kvp.Value.EntityType, kvp.Value);
            }
        }
    }
}
