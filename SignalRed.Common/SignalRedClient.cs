using Microsoft.AspNetCore.SignalR.Client;
using SignalRed.Common.Hubs;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace SignalRed.Client
{
    // TODOs
    // This client should have some sort of setting that defines whether it's
    // the game authority? Only the game authority should have the right to kick
    // players or force screen transitions. Alternatively, this could be up to the
    // implementing party and this client just dumbly allows any kind of well-formed
    // message?

    public delegate void SignalRedEvent<T>(T? message);
    public delegate void SignalRedEvent();

    public class SignalRedClient
    {
        private static SignalRedClient instance;
        private HubConnection? gameHub;
        private bool initialized = false;
        private string user = "new user";
        private ulong localEntityIndex = 0;
        private string clientId;

        /// <summary>
        /// An event fired when the connection to the server is closed
        /// </summary>
        public event SignalRedEvent<Exception>? ConnectionClosed;
        /// <summary>
        /// An event fired when the connection to the server is opened
        /// </summary>
        public event SignalRedEvent? ConnectionOpened;
        /// <summary>
        /// An event fired when a connection should be added or updated
        /// </summary>
        public event SignalRedEvent<ConnectionMessage>? ConnectionUpdateReceived;
        /// <summary>
        /// An event fired when a connection should be deleted
        /// </summary>
        public event SignalRedEvent<ConnectionMessage>? ConnectionDeleteReceived;
        /// <summary>
        /// An event fired when the local list of connections should be reckoned against
        /// the server's list
        /// </summary>
        public event SignalRedEvent<List<ConnectionMessage>>? ConnectionReckonReceived;
        /// <summary>
        /// An event fired when the client should transition to a new screen
        /// </summary>
        public event SignalRedEvent<ScreenMessage>? ScreenTransitionReceived;
        /// <summary>
        /// An event fired when the client should create an entity
        /// </summary>
        public event SignalRedEvent<EntityStateMessage>? EntityCreateReceived;
        /// <summary>
        /// An event fired when the client should update an entity
        /// </summary>
        public event SignalRedEvent<EntityStateMessage>? EntityUpdateReceived;
        /// <summary>
        /// An event fired when the client should delete an entity
        /// </summary>
        public event SignalRedEvent<EntityStateMessage>? EntityDeleteReceived;
        /// <summary>
        /// An event fired when the client should reckon its local entities with the list
        /// from the server
        /// </summary>
        public event SignalRedEvent<List<EntityStateMessage>>? EntityReckonReceived;
        /// <summary>
        /// An event fired when a generic message is received
        /// </summary>
        public event SignalRedEvent<GenericMessage>? GenericMessageReceived;

        



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
        public async Task Connect(string url, string username)
        {
            var uri = new Uri(url);
            await Connect(uri, username);
        }
        public async Task Connect(Uri url)
        {
            if (!initialized)
            {
                throw new Exception("Attempted to Connect without initializing SRClient service!");
            }

            // first disconnect from anything we're already connected to!
            await Disconnect();

            // now build our hub connection
            var gameHubUrl = new Uri(url, "game");
            gameHub = new HubConnectionBuilder()
                .WithUrl(gameHubUrl)
                .Build();

            RegisterHubHandlers();

            // form the connection
            await gameHub.StartAsync();
            Connected = true;

            // regiser our unique clientId against our connection ID
            // with the server
            await TryInvoke(nameof(GameHub.RegisterConnection), 
                new ConnectionMessage(this.ClientId, this.ConnectionId));

            // register events when server messages are received
            RegisterHubHandlers();

            // fire the successful connection event
            ConnectionOpened?.Invoke();
        }

        /// <summary>
        /// Disconnects from the server if a connection is active
        /// </summary>
        /// <returns></returns>
        public async Task Disconnect()
        {
            if (!initialized || !Connected || gameHub == null) return;

            await TryInvoke(nameof(GameHub.DeleteConnection),
                new ConnectionMessage(ClientId, ConnectionId));

            await gameHub.StopAsync();
            gameHub = null;
            Connected = false;
        }



        /// <summary>
        /// Makes a request to the server to transition screens
        /// </summary>
        /// <param name="screenName">The name of the screen to transition to</param>
        public async Task RequestScreenTransition(string screenName)
        {
            var msg = new ScreenMessage(ClientId, ConnectionId, screenName);
            await TryInvoke(nameof(GameHub.MoveToScreen), msg);
        }

        /// <summary>
        /// Requests that the server re-sends a move-to-screen event
        /// to this client, which will trigger a screen change on the IGameClient
        /// </summary>
        public async Task RequestCurrentScreen()
        {
            await TryInvoke(nameof(GameHub.RequestCurrentScreen));
        }



        /// <summary>
        /// Sends a generic message to all clients. Used to send one-off messages that fall
        /// outside of existing patterns.
        /// </summary>
        /// <param name="key">The message key</param>
        /// <param name="value">The message value</param>
        public async Task SendGenericMessage(string key, string value)
        {
            var msg = new GenericMessage(ClientId, ConnectionId, key, value);
            await TryInvoke(nameof(GameHub.SendGenericMessage), msg);
        }


        /// <summary>
        /// Registers an entity with the server. Expects the parameter to
        /// be a serializable state. Will create a unique ID for the entity
        /// that is based on this ClientId
        /// </summary>
        /// <typeparam name="T">The type of state</typeparam>
        /// <param name="initialState">The initial state the entity should be 
        /// created in. This will be passed to all clients to create the entity</param>
        public async Task RegisterEntity<T>(T initialState)
        {
            var entityId = GetUniqueEntityId();
            var msg = new EntityStateMessage(ClientId, ConnectionId, entityId);
            msg.SetState(initialState);
            await TryInvoke(nameof(GameHub.RegisterEntity), msg);
        }

        /// <summary>
        /// Gets a state from the provided entity and sends an update message to the server
        /// based on the current state.
        /// </summary>
        /// <typeparam name="T">The type of entity</typeparam>
        /// <param name="entity">The entity to update</param>
        /// <returns></returns>
        public async Task UpdateEntity<T>(T entity) where T : INetworkEntity
        {
            var msg = new EntityStateMessage(ClientId, ConnectionId, entity.EntityId);
            msg.SetState(entity.GetState<T>());
            await TryInvoke(nameof(GameHub.UpdateEntity), msg);
        }

        /// <summary>
        /// Gets a state from the provided entity and sends a delete message to the server
        /// based on the current state
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task DeleteEntity<T>(T entity) where T : INetworkEntity
        {
            var msg = new EntityStateMessage(ClientId, ConnectionId, entity.EntityId);
            msg.SetState(entity.GetState<T>());
            await TryInvoke(nameof(GameHub.DeleteEntity), msg);
        }

        /// <summary>
        /// Requests an entity reckoning message from the server
        /// </summary>
        /// <returns></returns>
        public async Task ReckonEntities()
        {
            await TryInvoke(nameof(GameHub.ReckonEntities));
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

            gameHub.Closed += (exception) => {
                ConnectionClosed?.Invoke(exception);
                return Task.CompletedTask;
            };

            gameHub.On<ConnectionMessage>(nameof(IGameClient.RegisterConnection),
                message => ConnectionUpdateReceived?.Invoke(message));
            gameHub.On<ConnectionMessage>(nameof(IGameClient.DeleteConnection),
                message => ConnectionDeleteReceived?.Invoke(message));
            gameHub.On<List<ConnectionMessage>>(nameof(IGameClient.ReckonConnections),
                message => ConnectionReckonReceived?.Invoke(message));

            gameHub.On<ScreenMessage>(nameof(IGameClient.MoveToScreen),
                message => ScreenTransitionReceived?.Invoke(message));

            gameHub.On<EntityStateMessage>(nameof(IGameClient.RegisterEntity),
                message => EntityCreateReceived?.Invoke(message));
            gameHub.On<EntityStateMessage>(nameof(IGameClient.UpdateEntity),
                message => EntityUpdateReceived?.Invoke(message));
            gameHub.On<EntityStateMessage>(nameof(IGameClient.DeleteEntity),
                message => EntityDeleteReceived?.Invoke(message));
            gameHub.On<List<EntityStateMessage>>(nameof(IGameClient.ReckonEntities),
                message => EntityReckonReceived?.Invoke(message));

            gameHub.On<GenericMessage>(nameof(IGameClient.ReceiveGenericMessage),
                message => GenericMessageReceived?.Invoke(message));

        }        
    }
}
