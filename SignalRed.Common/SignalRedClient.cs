using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.SignalR.Client;
using SignalRed.Common.Hubs;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Collections.Concurrent;
using static System.Formats.Asn1.AsnWriter;

namespace SignalRed.Client
{
    // TODOs
    // - Add timestamps to messages
    // - Add ability to simulate latency
    // - Test interpolation and prediction with timestamps
    // - Figure out authoritative pattern
    // - Make INetworkEntity use generics instead of objects that must be cast

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
        double lastRoundtripRequestTime;
        double lastServerTime;
        Random rand = new Random();
        List<double> roundtripSamples = new List<double>();
        ConcurrentQueue<Tuple<EntityStateMessage, SignalRedMessageType>> entities = new ConcurrentQueue<Tuple<EntityStateMessage, SignalRedMessageType>>();
        ConcurrentQueue<Tuple<NetworkMessage, SignalRedMessageType>> connections = new ConcurrentQueue<Tuple<NetworkMessage, SignalRedMessageType>>();
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
        /// How many roundtrip offsets we should average to guess our local time offset
        /// from the server time
        /// </summary>
        public int RoundtripSamplesToCollect { get; set; } = 10;

        /// <summary>
        /// How often to sample the roundtrip time between this client and the server
        /// </summary>
        public float RoundtripSampleFrequencySeconds { get; set; } = 0.5f;

        /// <summary>
        /// How frequently the client should reckon its lists with the server
        /// </summary>
        public float ReckonFrequencySeconds { get; set; } = 3f;

        /// <summary>
        /// Only implemented in DEBUG builds, adds delay between 0 and value
        /// to every request to simulate random latency for tolerance testing.
        /// </summary>
        public float DebugMaxSimulatedLatencyMilliseconds { get; set; } = 0f;

        /// <summary>
        /// The connected or disconnected status of the client.
        /// </summary>
        public bool Connected { get; private set; } = false;

        /// <summary>
        /// The average time it takes in milliseconds for a message from
        /// this client to reach the server
        /// </summary>
        public double Ping => roundtripSamples.Count > 0 ? roundtripSamples.Average() / 2d : 0d;

        /// <summary>
        /// The estimated offset between client and server time in milliseconds.
        /// Calculated by averaging multiple roundtrip time samples.
        /// </summary>
        public double ServerTimeOffset { get; private set; } = 0;

        /// <summary>
        /// The estimated precise time on the server. This is used to 
        /// </summary>
        public double ServerTime => GameHub.UnixTimeMilliseconds + ServerTimeOffset;
            


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
        /// Updates this service, which performs maintenance tasks such as
        /// measuring the roundtrip message time to the server. This should
        /// be called in the game client at a root level every tick.
        /// </summary>
        public void Update()
        {
            // EARLY OUT: not initialized
            if(!initialized)
            {
                return;
            }

            if(Connected)
            {
                if(lastRoundtripRequestTime < GameHub.UnixTimeMilliseconds - (RoundtripSampleFrequencySeconds * 1000))
                {
                    RequestServerTime();
                }
            }
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
            var msg = new ScreenMessage(ClientId, ConnectionId, ServerTime, screenName);
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
            var msg = new GenericMessage(ClientId, ConnectionId, ServerTime, key, value);
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
            var msg = new EntityStateMessage(ClientId, ConnectionId, ServerTime, entityId, ClientId);
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
        public void UpdateEntity<T>(ISignalRedEntity<T> entity)
        {
            var msg = new EntityStateMessage(ClientId, ConnectionId, ServerTime, entity.EntityId, entity.OwnerClientId);
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
        public void DeleteEntity<T>(ISignalRedEntity<T> entity)
        {
            var msg = new EntityStateMessage(ClientId, ConnectionId, ServerTime, entity.EntityId, entity.OwnerClientId);
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
        public List<Tuple<Common.Messages.NetworkMessage, SignalRedMessageType>> GetConnectionMessages() => ThreadsafeCopyAndClearQueue(connections);
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
        /// Sends a request to the server for its time so we can track roundtrip time and measure
        /// our average time delta from the server.
        /// </summary>
        void RequestServerTime()
        {
            lastRoundtripRequestTime = GameHub.UnixTimeMilliseconds;
            _ = TryInvoke(nameof(GameHub.RequestServerTime));
        }
        /// <summary>
        /// Recalculates our offset from server time, which is used
        /// to guess at a synchronized network time.
        /// </summary>
        /// <param name="receivedServerTime">A time we just received from the server in response to a request</param>
        void UpdateServerTimeOffset(double receivedServerTime)
        {
            // update the most recent server time we received
            lastServerTime = receivedServerTime;

            // figure out the most recent roundtrip time
            var lastRoundtrip = GameHub.UnixTimeMilliseconds - lastRoundtripRequestTime;

            // update our roundtrip samples collection
            roundtripSamples.Add(lastRoundtrip);
            while(roundtripSamples.Count > RoundtripSamplesToCollect)
            {
                roundtripSamples.RemoveAt(0);
            }

            // guess what time it was on the server the last time we
            // sent a time request by taking the time we got back and
            // subtracting the time it should have taken for our request
            // to reach to the server
            var lastAdjustedServerTime = lastServerTime - Ping;

            // now we can calculate our offset from the server time and use
            // this to calculate the server time at any given moment
            ServerTimeOffset = lastRoundtripRequestTime - lastAdjustedServerTime;
        }
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
                new Common.Messages.NetworkMessage(this.ClientId, this.ConnectionId, this.ServerTime));

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
                new Common.Messages.NetworkMessage(ClientId, ConnectionId, ServerTime));

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
            await ApplyDebugLatency();
            await gameHub.InvokeAsync(methodName, message);
            return true;
        }
        async Task<bool> TryInvoke(string methodName)
        {
            if (!initialized || !Connected || gameHub == null) return false;
            await ApplyDebugLatency();
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

            // handle measuring roundtrip time and server time offset
            gameHub.On<double>(nameof(IGameClient.ReceiveServerTime), time => UpdateServerTimeOffset(time));

            // handle incoming connection messages
            gameHub.On(nameof(IGameClient.CreateConnection),
                (Common.Messages.NetworkMessage                 message) => connections.Enqueue(Tuple.Create(message, SignalRedMessageType.Create)));
            gameHub.On(nameof(IGameClient.DeleteConnection),
                (Common.Messages.NetworkMessage                 message) => connections.Enqueue(Tuple.Create(message, SignalRedMessageType.Delete)));
            gameHub.On(nameof(IGameClient.ReckonConnections), (List<Common.Messages.NetworkMessage> messages) =>
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

        async Task ApplyDebugLatency()
        {
#if DEBUG
            var latency = (int)(rand.NextDouble() * DebugMaxSimulatedLatencyMilliseconds);
            await Task.Delay(latency);
#else
            // NOOP
#endif
        }
    }
}
