﻿using Microsoft.AspNetCore.SignalR.Client;
using SignalRed.Common.Hubs;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Collections.Concurrent;

namespace SignalRed.Client
{
    // TODOs
    // This client should have some sort of setting that defines whether it's
    // the game authority? Only the game authority should have the right to kick
    // players or force screen transitions. Alternatively, this could be up to the
    // implementing party and this client just dumbly allows any kind of well-formed
    // message?

    public enum SignalRedMessageType
    {
        Unknown = 0,
        Create = 1,
        Reckon = 2,
        Update = 3,
        Delete = 4,
    };

    public class SignalRedClient
    {
        private static SignalRedClient instance;
        private HubConnection? gameHub;
        private bool initialized = false;
        private string user = "new user";
        private ulong localEntityIndex = 0;
        private string clientId;

        ConcurrentQueue<Tuple<EntityStateMessage, SignalRedMessageType>> entities = new ConcurrentQueue<Tuple<EntityStateMessage, SignalRedMessageType>>();
        ConcurrentQueue<Tuple<ConnectionMessage, SignalRedMessageType>> connections = new ConcurrentQueue<Tuple<ConnectionMessage, SignalRedMessageType>>();
        ConcurrentQueue<GenericMessage> genericMessages = new ConcurrentQueue<GenericMessage>();
        ScreenMessage currentScreen = new ScreenMessage();


        /// <summary>
        /// The singleton instance of this service.
        /// </summary>
        public static SignalRedClient Instance => instance ?? (instance = new SignalRedClient());

        /// <summary>
        /// A unique identifier that represents this client's connection to the server. This
        /// value will change when disconnecting and reconnecting.
        /// </summary>
        public string ConnectionId => gameHub?.ConnectionId ?? "";

        /// <summary>
        /// A unique identifier that represents this client's identifier. This value persists as
        /// long as the client is running, even across disconnection and reconnection. It can be
        /// used to resume a game's progress across a disconnection event.
        /// </summary>
        public string ClientId => clientId;

        /// <summary>
        /// How frequently the client should reckon its lists with the server
        /// </summary>
        public float ReckonFrequencySeconds = 3f;

        /// <summary>
        /// The connected or disconnected status of the client.
        /// </summary>
        public bool Connected { get; private set; } = false;


        /// <summary>
        /// Initializes the client service
        /// </summary>
        /// <param name="gameClient">The game client, usually the game engine, which
        /// must implement IGameClient</param>
        public void Initialize()
        {
            clientId = Guid.NewGuid().ToString("N");
            localEntityIndex = 0;
            initialized = true;
        }
        /// <summary>
        /// Used to get a unique entity ID when creating
        /// a new entity. The ID is a combination of the 
        /// ClientId and an incrementing integer
        /// </summary>
        public string GetUniqueEntityId()
        {
            localEntityIndex++;
            return $"{ClientId}_{localEntityIndex}";
        }

        /// <summary>
        /// Connects to a server at the provided URL, which should
        /// include the protocol (http) and port number
        /// </summary>
        /// <param name="url">The url to connect to, including protocol and port number</param>
        /// <param name="onConnectedCallback">An optional Action to perform when the connection is complete</param>
        public void Connect(string url, Action? onConnectedCallback = null)
        {
            var uri = new Uri(url);
            _ = ConnectAsync(uri, onConnectedCallback);
        }
        /// <summary>
        /// Disconnects from the server if connected, and calls the provided
        /// callback once disconnected.
        /// </summary>
        /// <param name="onDisconnectedCallback">An optional Action to perform when the disconnection is complete</param>
        public void Disconnect(Action? onDisconnectedCallback = null)
        {
            _ = DisconnectAsync(onDisconnectedCallback);
        }


        /// <summary>
        /// Makes a request to the server to transition screens
        /// </summary>
        /// <param name="screenName">The name of the screen to transition to</param>
        public void RequestScreenTransition(string screenName)
        {
            var msg = new ScreenMessage(ClientId, ConnectionId, screenName);
            _ = TryInvoke(nameof(GameHub.MoveToScreen), msg);
        }
        /// <summary>
        /// Requests that the server re-sends a move-to-screen event
        /// to this client, which will trigger a screen change on the IGameClient
        /// </summary>
        public void RequestResendScreenTransition()
        {
            _ = TryInvoke(nameof(GameHub.RequestCurrentScreen));
        }


        /// <summary>
        /// Sends a generic message to all clients. Used to send one-off messages that fall
        /// outside of existing patterns.
        /// </summary>
        /// <param name="key">The message key</param>
        /// <param name="value">The message value</param>
        public void CreateGenericMessage(string key, string value)
        {
            var msg = new GenericMessage(ClientId, ConnectionId, key, value);
            _ = TryInvoke(nameof(GameHub.CreateGenericMessage), msg);
        }


        /// <summary>
        /// Registers an entity with the server. Expects the parameter to
        /// be a serializable state. Will create a unique ID for the entity
        /// that is based on this ClientId
        /// </summary>
        /// <typeparam name="T">The type of state</typeparam>
        /// <param name="initialState">The initial state the entity should be 
        /// created in. This will be passed to all clients to create the entity</param>
        public void CreateEntity<T>(T initialState)
        {
            var entityId = GetUniqueEntityId();
            var msg = new EntityStateMessage(ClientId, ConnectionId, entityId, ClientId);
            msg.SetState(initialState);
            _ = TryInvoke(nameof(GameHub.CreateEntity), msg);
        }
        /// <summary>
        /// Gets a state from the provided entity and sends an update message to the server
        /// based on the current state.
        /// </summary>
        /// <typeparam name="T">The type of entity</typeparam>
        /// <param name="entity">The entity to update</param>
        /// <returns></returns>
        public void UpdateEntity<T>(T entity) where T : INetworkEntity
        {
            var msg = new EntityStateMessage(ClientId, ConnectionId, entity.EntityId, entity.OwnerClientId);
            var state = entity.GetState();
            msg.SetState(state);
            _ = TryInvoke(nameof(GameHub.UpdateEntity), msg);
        }
        /// <summary>
        /// Gets a state from the provided entity and sends a delete message to the server
        /// based on the current state
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public void DeleteEntity<T>(T entity) where T : INetworkEntity
        {
            var msg = new EntityStateMessage(ClientId, ConnectionId, entity.EntityId, entity.OwnerClientId);
            var state = entity.GetState();
            msg.SetState(state);
            _ = TryInvoke(nameof(GameHub.DeleteEntity), msg);
        }
        /// <summary>
        /// Requests an entity reckoning message from the server
        /// </summary>
        /// <returns></returns>
        public void ReckonEntities()
        {
            _ = TryInvoke(nameof(GameHub.ReckonEntities));
        }


        /// <summary>
        /// Gets a list of entity messages that have arrived since the last frame
        /// </summary>
        /// <returns>A list of tuples containing messages and the type of message</returns>
        public List<Tuple<EntityStateMessage, SignalRedMessageType>> GetEntityMessages() => ThreadsafeCopyAndClearQueue(entities);
        /// <summary>
        /// Gets a list of connection messages that have arrived since the last frame
        /// </summary>
        /// <returns>A list of tuples containing messages and the type of message</returns>
        public List<Tuple<ConnectionMessage, SignalRedMessageType>> GetConnectionMessages() => ThreadsafeCopyAndClearQueue(connections);
        /// <summary>
        /// Gets a list of generic messages that have arrived since the last frame
        /// </summary>
        /// <returns>A list of generic messages</returns>
        public List<GenericMessage> GetGenericMessages() => ThreadsafeCopyAndClearQueue(genericMessages);
        /// <summary>
        /// Gets the current screen
        /// </summary>
        /// <returns></returns>
        public ScreenMessage GetCurrentScreen() => currentScreen;


        /// <summary>
        /// Connects to the provided server URL and calls
        /// the provided callback once connected
        /// </summary>
        /// <param name="url">The server URL including port</param>
        /// <param name="onConnectedCallback">A callback to call once connected</param>
        /// <exception cref="Exception">Thrown if called before initializing this service.</exception>
        async Task ConnectAsync(Uri url, Action? onConnectedCallback = null)
        {
            if (!initialized)
            {
                throw new Exception("Attempted to Connect without initializing SRClient service!");
            }

            // first disconnect from anything we're already connected to!
            await DisconnectAsync();

            // now build our hub connection
            var gameHubUrl = new Uri(url, "game");
            gameHub = new HubConnectionBuilder()
                .WithUrl(gameHubUrl)
                .Build();

            // register our incoming message handlers
            RegisterHubHandlers();

            // form the connection
            await gameHub.StartAsync();
            Connected = true;

            // regiser our unique clientId against our connection ID
            // with the server
            await TryInvoke(nameof(GameHub.CreateConnection),
                new ConnectionMessage(this.ClientId, this.ConnectionId));

            // fire the successful connection event
            onConnectedCallback?.Invoke();
        }
        /// <summary>
        /// Disconnects from the server if a connection is active and
        /// calls the provided callback once disconnected.
        /// </summary>
        async Task DisconnectAsync(Action? onDisconnectedCallback = null)
        {
            // EARLY OUT: we're not connected
            if (!initialized || !Connected || gameHub == null)
            {
                onDisconnectedCallback?.Invoke();
                return;
            }

            await TryInvoke(nameof(GameHub.DeleteConnection),
                new ConnectionMessage(ClientId, ConnectionId));

            await gameHub.StopAsync();
            gameHub = null;
            Connected = false;

            onDisconnectedCallback?.Invoke();
        }
        /// <summary>
        /// Attempts to invoke the provided method name on the hub.
        /// 
        /// Overloads for a message argument or no argument.
        /// </summary>
        /// <param name="methodName">The name of the remote method to invoke</param>
        async Task<bool> TryInvoke<T>(string methodName, T message)
        {
            if (!initialized || !Connected || gameHub == null) return false;
            await gameHub.InvokeAsync(methodName, message);
            return true;
        }
        async Task<bool> TryInvoke(string methodName)
        {
            if (!initialized || !Connected || gameHub == null) return false;
            await gameHub.InvokeAsync(methodName);
            return true;
        }
        /// <summary>
        /// A threadsafe way to dequeue all items in a concurrent queue to a list
        /// and then return the list. This is used to convert a queue of items that
        /// were added asynchronously by server RPCs, to a synchronous method of fetching
        /// all messages since the last frame in the game client.
        /// </summary>
        /// <typeparam name="T">The queue and list type</typeparam>
        /// <param name="queue">The concurrent queue</param>
        /// <returns>A list of all of the items that were in the queue</returns>
        List<T> ThreadsafeCopyAndClearQueue<T>(ConcurrentQueue<T> queue)
        {
            var listCopy = new List<T>();
            T item;
            while (queue.TryDequeue(out item))
            {
                listCopy.Add(item);
            }
            return listCopy;
        }
        /// <summary>
        /// Registers all of the handlers for various message types the
        /// hub can handle
        /// </summary>
        void RegisterHubHandlers()
        {
            // EARLY OUT: bad gamehub
            if (gameHub == null)
            {
                return;
            }

            gameHub.Closed += (exception) =>
            {
                Connected = false;
                // TODO: how to notify game client that connection failed?
                return Task.CompletedTask;
            };

            // handle incoming connection messages
            gameHub.On<ConnectionMessage>(nameof(IGameClient.CreateConnection),
                message => connections.Enqueue(Tuple.Create(message, SignalRedMessageType.Create)));
            gameHub.On<ConnectionMessage>(nameof(IGameClient.DeleteConnection),
                message => connections.Enqueue(Tuple.Create(message, SignalRedMessageType.Delete)));
            gameHub.On<List<ConnectionMessage>>(nameof(IGameClient.ReckonConnections), messages =>
            {
                foreach (var message in messages)
                {
                    connections.Enqueue(Tuple.Create(message, SignalRedMessageType.Reckon));
                }
            });

            // handle incoming entity messages
            gameHub.On<EntityStateMessage>(nameof(IGameClient.CreateEntity),
                message => entities.Enqueue(Tuple.Create(message, SignalRedMessageType.Create)));
            gameHub.On<EntityStateMessage>(nameof(IGameClient.UpdateEntity),
                message => entities.Enqueue(Tuple.Create(message, SignalRedMessageType.Update)));
            gameHub.On<EntityStateMessage>(nameof(IGameClient.DeleteEntity),
                message => entities.Enqueue(Tuple.Create(message, SignalRedMessageType.Delete)));
            gameHub.On<List<EntityStateMessage>>(nameof(IGameClient.ReckonEntities), messages =>
            {
                foreach (var message in messages)
                {
                    entities.Enqueue(Tuple.Create(message, SignalRedMessageType.Reckon));
                }
            });

            // handle incoming generic messages
            gameHub.On<GenericMessage>(nameof(IGameClient.ReceiveGenericMessage),
                message => genericMessages.Enqueue(message));

            // handle incoming screen message
            gameHub.On<ScreenMessage>(nameof(IGameClient.MoveToScreen),
                message => currentScreen = message);






        }
    }
}
