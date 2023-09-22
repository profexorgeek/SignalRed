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
        static List<ConnectionMessage> connections = new List<ConnectionMessage>();
        static List<PayloadMessage> payloads = new List<PayloadMessage>();
        static ScreenMessage currentScreen = new ScreenMessage("None");

        /// <summary>
        /// Called by a client when it wants all clients to move
        /// to the target screen.
        /// </summary>
        /// <param name="message">A message containing the target screen</param>
        public async Task MoveToScreen(ScreenMessage message)
        {
            Console.WriteLine($"Received request to move to screen: {message.TargetScreen}");
            currentScreen = message;
            await Clients.All.MoveToScreen(message);
        }
        
        /// <summary>
        /// Called by a client when it wants to know what screen it should be on
        /// </summary>
        /// <returns></returns>
        public async Task RequestCurrentScreen()
        {
            await Clients.Caller.MoveToScreen(currentScreen);
        }


        /// <summary>
        /// Called after connecting to associate a durable ClientId with
        /// a transient ConnectionId
        /// </summary>
        /// <param name="message">The connection to register</param>
        public async Task RegisterConnection(ConnectionMessage message)
        {
            var existing = connections.Where(c => c.SenderId == message.SenderId).FirstOrDefault();
            
            // we already knew about this client, update the connection ID in case this is a
            // dropped client that has reconnected
            if(existing != null)
            {
                existing.SenderConnectionId = message.SenderConnectionId;
            }

            // this is a new user
            else
            {
                connections.Add(message);
            }

            await Clients.All.RegisterConnection(message);
        }

        /// <summary>
        /// Called to fetch a fresh list of all valid connections
        /// </summary>
        public async Task FetchConnections()
        {
            CleanConnectionList();
            var validConnections = connections.Where(c => c.SenderConnectionId != null).ToList();
            await Clients.Caller.ReckonConnections(validConnections);
        }

        /// <summary>
        /// Called to delete a connection, usually before a graceful disconnect.
        /// </summary>
        /// <param name="message">The connection to delete.</param>
        public async Task DeleteConnection(ConnectionMessage message)
        {
            var existing = connections.Where(c => c.SenderConnectionId == message.SenderConnectionId).FirstOrDefault();
            if(existing != null)
            {
                connections.Remove(existing);
            }
            await Clients.All.DeleteConnection(message);
        }


        /// <summary>
        /// Called to broadcast a generic message.
        /// </summary>
        /// <param name="message">The generic message</param>
        public async Task SendGenericMessage(GenericMessage message)
        {
            await Clients.All.ReceiveGenericMessage(message);
        }


        public async RegisterPayload(PayloadMessage message)
        {
            if(message.TargetId != null)
            {

            }
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

        public async Task CreateEntity(PayloadMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.TargetId)) return;
            var existing = entities.Where(e => e.TargetId == message.TargetId).FirstOrDefault();
            if(existing == null)
            {
                entities.Add(message);
                await Clients.All.CreateEntity(message);
            }
        }
        public async Task UpdateEntity(PayloadMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.TargetId)) return;
            var existing = entities.Where(e => e.TargetId == message.TargetId).FirstOrDefault();
            if(existing != null)
            {
                entities.Remove(existing);
                entities.Add(message);

                await Clients.All.UpdateEntity(message);
            }
        }
        public async Task DeleteEntity(PayloadMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.TargetId)) return;
            var existing = entities.Where(e => e.TargetId == message.TargetId).FirstOrDefault();
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

        public async Task SendGenericMessage(GenericMessage message)
        {
            await Clients.All.ReceiveGenericMessage(message);
        }

        /// <summary>
        /// Cleans the user list, removing any users that aren't found
        /// in the connected clients
        /// </summary>
        void CleanConnectionList()
        {
            // TODO: remove long-dead connections from the list

            for (var i = connections.Count - 1; i > -1; i--)
            {
                if (Clients.Client(connections[i].SenderConnectionId) == null)
                {
                    connections[i].SenderConnectionId = null;
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
                if(users.Any(u => u.ClientId == entity.ClientId) == false)
                {
                    entities.RemoveAt(i);
                }
            }
        }
    }
}
