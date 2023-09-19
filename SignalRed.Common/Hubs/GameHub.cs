using Microsoft.AspNetCore.SignalR;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

namespace SignalRed.Common.Hubs
{
    public class GameHub : Hub<IGameClient>
    {
        static List<ChatMessage> messages = new List<ChatMessage>();
        static Dictionary<string, string> users = new Dictionary<string, string>();
        static Dictionary<string, INetworkEntityState> entities = new Dictionary<string, INetworkEntityState>();
        static string currentScreen = "";

        /// <summary>
        /// Notifies all clients that they should move to the
        /// provided screen name.
        /// </summary>
        /// <param name="screenName">The target screen name to move to</param>
        public async Task MoveToScreen(string screenName)
        {
            Console.WriteLine($"Received request to move to screen: {screenName}");
            currentScreen = screenName;
            await Clients.All.MoveToScreen(screenName);
        }
        public async Task ReceiveCurrentScreen()
        {
            await Clients.Caller.MoveToScreen(currentScreen);
        }


        /// <summary>
        /// Tracks relationship between connection ID and
        /// human-readable user name. Notifies all clients
        /// that 
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task RegisterUser(string username)
        {
            var id = Context.ConnectionId;
            if(users.ContainsKey(id))
            {
                users[id] = username;
            }
            else
            {
                users.Add(Context.ConnectionId, username);
            }
            
            Console.WriteLine("User registered: " + username);
            await Clients.All.RegisterUser(id, username);
        }

        /// <summary>
        /// Provides list of all users and their connection ID
        /// to the calling client
        /// </summary>
        public async Task ReceiveAllUsers()
        {
            Console.WriteLine("Received request for all users");
            await Clients.Caller.ReceiveAllUsers(users);
        }



        /// <summary>
        /// Distributes the incoming message to all clients, including
        /// the caller.
        /// </summary>
        /// <param name="message">The incoming message</param>
        public async Task ReceiveMessage(ChatMessage message)
        {
            messages.Add(message);
            Console.WriteLine("Message received: " + message);
            await Clients.All.ReceiveMessage(message);
            
        }

        /// <summary>
        /// Provides the caller with all chat messages that have
        /// occurred since the hub was started
        /// </summary>
        public async Task ReceiveAllMessages()
        {
            Console.WriteLine("Received request for all messages");
            await Clients.Caller.ReceiveAllMessages(messages);
        }



        /// <summary>
        /// Notifies all clients that they should update the 
        /// provided entity.
        /// </summary>
        /// <typeparam name="T">The type of entity to update</typeparam>
        /// <param name="entityToUpdate">The entity to update</param>
        public async Task UpdateEntity(string typeString, JsonElement entityElement)
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
