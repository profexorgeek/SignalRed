using Microsoft.AspNetCore.SignalR.Client;
using SignalRed.Common.Hubs;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;

namespace SignalRed.Client
{
    public class SRClient
    {
        private static SRClient instance;
        private HubConnection? gameHub;
        private bool initialized = false;
        private string user = "new user";
        private IGameClient gameClient;
        private ulong localEntityIndex = 0;

        public static SRClient Instance => instance ?? (instance = new SRClient());
        public string? ClientId => gameHub?.ConnectionId;
        public string CurrentScreen { get; private set; } = "";
        public bool Connected { get; private set; } = false;

        /// <summary>
        /// Initializes the client service
        /// </summary>
        /// <param name="gameClient">The game client, usually the game engine, which
        /// must implement IGameClient</param>
        public void Initialize(IGameClient _gameClient)
        {
            localEntityIndex = 0;
            gameClient = _gameClient;
            initialized = true;
        }

        /// <summary>
        /// Connects to a server at the provided URL, which should
        /// include the protocol (http) and port number
        /// </summary>
        /// <param name="url">The url to connect to, including protocol and port number</param>
        public async Task ConnectAsync(Uri url, string username)
        {
            if (!initialized)
            {
                throw new Exception("Attempted to Connect without initializing SRClient service!");
            }

            // first, break any existing connections
            await DisconnectAsync();

            // now build our hub connection
            var gameHubUrl = new Uri(url, "game");
            gameHub = new HubConnectionBuilder()
                .WithUrl(gameHubUrl)
                .Build();

            RegisterHubHandlers();

            // form the connection
            await gameHub.StartAsync();

            // post connection state setup: register username, fetch all messages,
            // users, current screen, and entities
            user = username;
            await gameHub.InvokeAsync(nameof(GameHub.RegisterUser), user);
            await gameHub.InvokeAsync(nameof(GameHub.ReceiveAllUsers));
            await gameHub.InvokeAsync(nameof(GameHub.ReceiveAllMessages));
            await gameHub.InvokeAsync(nameof(GameHub.ReceiveCurrentScreen));
            await gameHub.InvokeAsync(nameof(GameHub.ReceiveAllEntities));

            Connected = true;
        }

        

        /// <summary>
        /// Used to get a unique entity ID when creating
        /// a new entity
        /// </summary>
        public string GetUniqueEntityId()
        {
            localEntityIndex++;
            return $"{ClientId}_{localEntityIndex}";
        }

        public async Task RequestCreateEntity<T>(T entityState)
        {
            if (!initialized || !Connected || gameHub == null) return;

            var msg = new EntityMessage()
            {
                OwnerId = ClientId,
                EntityId = GetUniqueEntityId(),
                EntityType = entityState.GetType().FullName,
                UpdateType = UpdateType.Create
            };
            msg.SetPayload(entityState);
        }

        

        /// <summary>
        /// Sends a chat message
        /// </summary>
        /// <param name="chatMessage">The message to send</param>
        /// <returns>A task representing the message send status</returns>
        public async Task SendMessageAsync(string chatMessage)
        {
            if (!initialized || !Connected || gameHub == null) return;
            await gameHub.InvokeAsync(nameof(GameHub.ReceiveMessage), new ChatMessage(
                gameHub.ConnectionId,
                user,
                chatMessage
                ));
        }

        /// <summary>
        /// Sends an entity update to the server
        /// </summary>
        /// <typeparam name="T">The type of entity</typeparam>
        /// <param name="entityUpdateMessage">The entity state to update</param>
        public async Task UpdateEntityAsync<T>(T entityUpdateMessage) where T : INetworkEntityState
        {
            if (!initialized || !Connected || gameHub == null) return;
            await gameHub.InvokeAsync(nameof(GameHub.UpdateEntity), entityUpdateMessage.EntityType, entityUpdateMessage);
        }

        /// <summary>
        /// Makes a request to the server to transition screens
        /// </summary>
        /// <param name="screenName">The name of the screen to transition to</param>
        public async Task RequestScreenTransition(string screenName)
        {
            if (!initialized || !Connected || gameHub == null) return;
            await gameHub.InvokeAsync(nameof(GameHub.MoveToScreen), screenName);
        }

        /// <summary>
        /// Disconnects from the server if a connection is active
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            if (gameHub != null)
            {
                await gameHub.StopAsync();
            }

            Connected = false;
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

            // bind connection failure exceptions
            gameHub.Closed += (exception) =>
            {
                return gameClient.FailConnection(exception);
            };

            // bind screen transitions
            gameHub.On<string>(nameof(IGameClient.MoveToScreen), screen =>
            {
                gameClient.MoveToScreen(screen);
            });


            // handle new user registration
            gameHub.On<string, string>(nameof(IGameClient.RegisterUser), (id, username) =>
            {
                gameClient.RegisterUser(id, username);
            });

            // handle all user registration
            gameHub.On<Dictionary<string, string>>(nameof(IGameClient.ReceiveAllUsers), users =>
            {
                gameClient.ReceiveAllUsers(users);
            });


            // bind incoming chats
            gameHub.On<ChatMessage>(nameof(IGameClient.ReceiveMessage), message =>
            {
                gameClient.ReceiveMessage(message);
            });

            // handle receiving all chat messages
            gameHub.On<List<ChatMessage>>(nameof(IGameClient.ReceiveAllMessages), messages =>
            {
                gameClient.ReceiveAllMessages(messages);
            });


            // handle receiving an entity update
            gameHub.On<string, object>(nameof(IGameClient.UpdateEntity), (typeString, entity) =>
            {
                gameClient.UpdateEntity(typeString, entity);
            });
        }
    }
}
