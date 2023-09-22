using Microsoft.AspNetCore.SignalR;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Linq;
using System.Linq.Expressions;

namespace SignalRed.Common.Hubs
{
    // TODO: when a client disconnects, we just wipe all of its entities
    // when reckoning. Should we offer the option to reassign them or 
    // give the user the ability to choose what to do here?

    public class GameHub : Hub<IGameClient>
    {
        static List<ConnectionMessage> connections = new List<ConnectionMessage>();
        static List<EntityStateMessage> entityStates = new List<EntityStateMessage>();
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
            var existing = connections.Where(c => c.SenderClientId == message.SenderClientId).FirstOrDefault();

            // we already knew about this client, update the connection ID in case this is a
            // dropped client that has reconnected
            if (existing != null)
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
        /// Called to delete a connection, usually before a graceful disconnect.
        /// </summary>
        /// <param name="message">The connection to delete.</param>
        public async Task DeleteConnection(ConnectionMessage message)
        {
            var existing = connections.Where(c => c.SenderConnectionId == message.SenderConnectionId).FirstOrDefault();
            if (existing != null)
            {
                connections.Remove(existing);
            }
            await Clients.All.DeleteConnection(message);
        }

        /// <summary>
        /// Called to fetch a fresh list of all valid connections
        /// </summary>
        public async Task ReckonConnections()
        {
            CleanConnectionList();
            var validConnections = connections.Where(c => c.SenderConnectionId != null).ToList();
            await Clients.Caller.ReckonConnections(validConnections);
        }


        /// <summary>
        /// Called to broadcast a generic message.
        /// </summary>
        /// <param name="message">The generic message</param>
        public async Task SendGenericMessage(GenericMessage message)
        {
            await Clients.All.ReceiveGenericMessage(message);
        }


        /// <summary>
        /// Registers or updates an entity state using EntityId as the unique key. This
        /// generally assumes that a payload represents a game entity state
        /// and every game entity should have a unique ID. TNote: this will replace an
        /// existing payload with the same ID.
        /// 
        /// Payloads that have an empty or null EntityId will not be saved on
        /// the server or passed in Reckoning. If you want a payload message that
        /// isn't tied to a unique game entity, it is recommended that you use
        /// GenericMessage instead.
        /// </summary>
        /// <param name="message">The payload message</param>
        /// <returns></returns>
        public async Task RegisterEntity(EntityStateMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.EntityId))
            {
                var existing = entityStates.Where(p => p.EntityId == message.EntityId).FirstOrDefault();
                // if we have an existing payload, we just remove it and replace it
                if (existing != null)
                {
                    entityStates.Remove(existing);
                }
                entityStates.Add(message);
            }
            await Clients.All.RegisterEntity(message);
        }

        /// <summary>
        /// Updates an entity state if it exists using EntityId as the unique key.
        /// Noop if it doesn't exist because we may have received an update 
        /// message after a destroy message and we don't want to accidentally 
        /// resurrect a dead entity.
        /// 
        /// Payloads that have an empty or null EntityId will not be saved on
        /// the server or passed in Reckoning. If you want a payload message that
        /// isn't tied to a unique game entity, it is recommended that you use
        /// GenericMessage instead.
        /// </summary>
        /// <param name="message">The payload to be updated</param>
        public async Task UpdateEntity(EntityStateMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.EntityId))
            {
                var existing = entityStates.Where(p => p.EntityId == message.EntityId).FirstOrDefault();
                // if we have an existing payload, we just remove it and replace it
                if (existing != null)
                {
                    entityStates.Remove(existing);
                    entityStates.Add(message);
                    await Clients.All.UpdateEntity(message);
                }
            }
        }

        /// <summary>
        /// Deletes an entity state if it exists using EntityId as the unique key.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task DeleteEntity(EntityStateMessage message)
        {
            if(!string.IsNullOrWhiteSpace(message.EntityId))
            {
                var existing = entityStates.Where(e => e.EntityId == message.EntityId).FirstOrDefault();
                if (existing != null)
                {
                    entityStates.Remove(existing);
                }
            }
            await Clients.All.DeleteEntity(message);
        }

        public async Task ReckonEntities()
        {
            CleanEntityStateList();
            await Clients.Caller.ReckonEntities(entityStates);
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
        void CleanEntityStateList()
        {
            for (var i = entityStates.Count - 1; i > -1; i--)
            {
                var entity = entityStates[i];
                if (connections.Any(u => u.SenderClientId == entity.OwnerId) == false)
                {
                    entityStates.RemoveAt(i);
                }
            }
        }
    }
}
