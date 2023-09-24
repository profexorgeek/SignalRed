using Microsoft.AspNetCore.SignalR;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Runtime.CompilerServices;

namespace SignalRed.Common.Hubs
{
    // TODO: when a client disconnects, we just wipe all of its entities
    // when reckoning. Should we offer the option to reassign them or 
    // give the user the ability to choose what to do here?

    public class GameHub : Hub<IGameClient>
    {
        static List<ConnectionMessage> connections = new List<ConnectionMessage>();
        static List<EntityStateMessage> entities = new List<EntityStateMessage>();
        static ScreenMessage currentScreen = new ScreenMessage("", "", "None");
        static SemaphoreSlim connectionsSemaphor = new SemaphoreSlim(1);
        static SemaphoreSlim entitiesSemaphor = new SemaphoreSlim(1);

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
        public async Task CreateConnection(ConnectionMessage message)
        {
            await connectionsSemaphor.WaitAsync();
            try
            {
                var existing = connections.Where(c => c.SenderClientId == message.SenderClientId).FirstOrDefault();

                // we already knew about this client, update the connection ID in case this is a
                // dropped client that has reconnected
                if (existing != null)
                {
                    existing.SenderConnectionId = message.SenderConnectionId;
                }
                // this is a new client
                else
                {
                    connections.Add(message);
                }
            }
            finally
            {
                connectionsSemaphor.Release();
            }
            
            Console.WriteLine($"Connection received: {message.SenderClientId}:{message.SenderConnectionId}");
            await Clients.All.CreateConnection(message);
        }

        /// <summary>
        /// Called to delete a connection, usually before a graceful disconnect.
        /// </summary>
        /// <param name="message">The connection to delete.</param>
        public async Task DeleteConnection(ConnectionMessage message)
        {
            await connectionsSemaphor.WaitAsync();
            try
            {
                var existing = connections.Where(c => c.SenderConnectionId == message.SenderConnectionId).FirstOrDefault();
                if (existing != null)
                {
                    connections.Remove(existing);
                }
            }
            finally
            {
                connectionsSemaphor.Release();
            }

            Console.WriteLine($"Connection removed: {message.SenderClientId}:{message.SenderConnectionId}");
            await Clients.All.DeleteConnection(message);
        }

        /// <summary>
        /// Called to fetch a fresh list of all valid connections
        /// </summary>
        public async Task ReckonConnections()
        {
            List<ConnectionMessage> tempConnections;
            await connectionsSemaphor.WaitAsync();
            try
            {
                for (var i = connections.Count - 1; i > -1; i--)
                {
                    if (Clients.Client(connections[i].SenderConnectionId) == null)
                    {
                        connections.RemoveAt(i);
                    }
                }
                tempConnections = connections.Where(c => c.SenderConnectionId != null).ToList();
            }
            finally
            {
                connectionsSemaphor.Release();
            }
            await Clients.Caller.ReckonConnections(tempConnections);
        }



        /// <summary>
        /// Called to broadcast a generic message.
        /// </summary>
        /// <param name="message">The generic message</param>
        public async Task CreateGenericMessage(GenericMessage message)
        {
            Console.WriteLine($"Generic received: {message.MessageKey}:{message.MessageValue}");
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
        public async Task CreateEntity(EntityStateMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.EntityId))
            {
                await entitiesSemaphor.WaitAsync();
                try
                {
                    var existing = entities.Where(p => p.EntityId == message.EntityId).FirstOrDefault();
                    // if we have an existing payload, we just remove it and replace it
                    if (existing != null)
                    {
                        entities.Remove(existing);
                    }
                    entities.Add(message);
                }
                finally
                {
                    entitiesSemaphor.Release();
                }

                Console.WriteLine($"Registered entity: {message.StateType}:{message.EntityId}");
                await Clients.All.CreateEntity(message);
            }
            else
            {
                throw new Exception($"Cannot create entity with null or empty unique ID! {message.StateType}:{message.EntityId}");
            }
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
        public async Task UpdateEntity(EntityStateMessage? message)
        {
            if (!string.IsNullOrWhiteSpace(message.EntityId))
            {
                await entitiesSemaphor.WaitAsync();
                try
                {
                    var existing = entities.Where(p => p.EntityId == message.EntityId).FirstOrDefault();
                    // if we have an existing payload, we just remove it and replace it
                    if (existing != null)
                    {
                        entities.Remove(existing);
                        entities.Add(message);
                    }
                }
                finally
                {
                    entitiesSemaphor.Release();
                }
                await Clients.All.UpdateEntity(message);
            }
            else
            {
                throw new Exception($"Cannot update an entity with no unique ID!");
            }
        }

        /// <summary>
        /// Deletes an entity state if it exists using EntityId as the unique key.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task DeleteEntity(EntityStateMessage? message)
        {
            if (!string.IsNullOrWhiteSpace(message.EntityId))
            {
                await entitiesSemaphor.WaitAsync();
                try
                {
                    var existing = entities.Where(e => e.EntityId == message.EntityId).FirstOrDefault();
                    if (existing != null)
                    {
                        entities.Remove(existing);
                    }
                }
                finally
                {
                    entitiesSemaphor.Release();
                }
                Console.WriteLine($"Deleted entity: {message.StateType}:{message.EntityId}");
                await Clients.All.DeleteEntity(message);
            }
            else
            {
                throw new Exception($"Cannot delete an entity with no unique ID!");
            }
            
        }

        /// <summary>
        /// Sends list of all entity states to the requesting client
        /// </summary>
        /// <returns></returns>
        public async Task ReckonEntities()
        {
            // to reckon entities we need to lock both the connections and entities semaphors
            // so that neither collection is changed by some other thread while we clean them both up
            // we also do an on-the-fly list cleanup of any entities that have no owning connection
            var entityListCopy = new List<EntityStateMessage>();
            await entitiesSemaphor.WaitAsync();
            try
            {
                await connectionsSemaphor.WaitAsync();
                try
                {
                    for (var i = entities.Count - 1; i > -1; i--)
                    {
                        var entity = entities[i];

                        if (connections.Any(u => u.SenderClientId == entity.OwnerClientId) == false)
                        {
                            entities.RemoveAt(i);
                        }
                        else
                        {
                            entityListCopy.Add(entity);
                        }
                    }
                }
                finally
                {
                    connectionsSemaphor.Release();
                }
            }
            finally
            {
                entitiesSemaphor.Release();
            }
            await Clients.Caller.ReckonEntities(entityListCopy);
        }
    }
}
