using Microsoft.AspNetCore.SignalR;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Runtime.CompilerServices;

namespace SignalRed.Common.Hubs
{
    // TODOs:
    // 1) Figure out what to do with leftover entities when a client disconnects unexpectedly

    public class GameHub : Hub<IGameClient>
    {
        public static double UnixTimeMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        static List<Messages.NetworkMessage> connections = new List<Messages.NetworkMessage>();
        static List<EntityStateMessage> entities = new List<EntityStateMessage>();
        static ScreenMessage currentScreen = new ScreenMessage("", "", UnixTimeMilliseconds, "None");
        static SemaphoreSlim connectionsSemaphor = new SemaphoreSlim(1);
        static SemaphoreSlim entitiesSemaphor = new SemaphoreSlim(1);

        /// <summary>
        /// Disconnects all clients, clears all entities, and restores server to
        /// a clean state.
        /// </summary>
        public async Task ResetServerStatus()
        {
            await Clients.All.FailConnection("Server was forced to restart.");

            await connectionsSemaphor.WaitAsync();
            try
            {
                connections.Clear();
            }
            finally
            {
                connectionsSemaphor.Release();
            }
            
            await entitiesSemaphor.WaitAsync();
            try
            {
                entities.Clear();
            }
            finally
            {
                entitiesSemaphor.Release();
            }

            currentScreen = new ScreenMessage("", "", UnixTimeMilliseconds, "None");
        }

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
        /// Called by a client to get the server's time. This is used to measure roundtrip time
        /// and timestamp messages.
        /// </summary>
        public async Task RequestServerTime(string requestId)
        {
            await Clients.Caller.ReceiveServerTime(requestId, UnixTimeMilliseconds);
        }


        /// <summary>
        /// Called after connecting to associate a durable ClientId with
        /// a transient ConnectionId
        /// </summary>
        /// <param name="message">The connection to register</param>
        public async Task CreateConnection(Messages.NetworkMessage message)
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
        public async Task DeleteConnection(Messages.NetworkMessage message)
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
            List<Messages.NetworkMessage> tempConnections;
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
        /// Updates an entity state if it exists using EntityId as the unique key
        /// IF the update received is newer. Stale timestamps are discarded and
        /// not forwarded on to clients. Noop if the EntityId doesn't exist 
        /// because we may have received an update  message after a destroy 
        /// message and we don't want to accidentally resurrect a dead entity.
        /// 
        /// Payloads that have an empty or null EntityId will not be saved on
        /// the server or passed in Reckoning. If you want a payload message that
        /// isn't tied to a unique game entity, it is recommended that you use
        /// GenericMessage instead.
        /// </summary>
        /// <param name="message">The payload to be updated</param>
        public async Task UpdateEntity(EntityStateMessage? message)
        {
            bool shouldSend = false;
            if (!string.IsNullOrWhiteSpace(message.EntityId))
            {
                await entitiesSemaphor.WaitAsync();
                try
                {
                    var existing = entities.Where(p => p.EntityId == message.EntityId).FirstOrDefault();
                    if (existing != null)
                    {
                        var staleMessage = existing.SendTime > message.SendTime;
                        if (staleMessage)
                        {
                            Console.WriteLine($"Discrading stale {message.StateType} message that is {existing.SendTime - message.SendTime} older.");
                        }
                        else
                        {
                            entities.Remove(existing);
                            entities.Add(message);
                            shouldSend = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Discarding {message.StateType} update for entity {message.EntityId} that doesn't exist on server");
                    }
                }
                finally
                {
                    entitiesSemaphor.Release();
                }
                if(shouldSend)
                {
                    await Clients.All.UpdateEntity(message);
                }
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
