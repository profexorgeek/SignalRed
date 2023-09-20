using Microsoft.AspNetCore.SignalR.Client;
using SignalRed.Common.Hubs;
using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System.Diagnostics;
using System.Net;

namespace SignalRed.Client
{
    // TODOs
    // This client should have some sort of setting that defines whether it's
    // the game authority? Only the game authority should have the right to kick
    // players or force screen transitions. Alternatively, this could be up to the
    // implementing party and this client just dumbly allows any kind of well-formed
    // message?

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
        /// Used to get a unique entity ID when creating
        /// a new entity
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
        public async Task Connect(Uri url, string username)
        {
            if (!initialized)
            {
                throw new Exception("Attempted to Connect without initializing SRClient service!");
            }

            // first, break any existing connections
            await Disconnect();

            // now build our hub connection
            var gameHubUrl = new Uri(url, "game");
            gameHub = new HubConnectionBuilder()
                .WithUrl(gameHubUrl)
                .Build();

            RegisterHubHandlers();

            // form the connection
            await gameHub.StartAsync();

            user = username;
            await UpdateUser(new UserMessage(ClientId, user));

            Connected = true;
        }
        public async Task Connect(string url, string username)
        {
            var uri = new Uri(url);
            await Connect(uri, username);
        }
        /// <summary>
        /// Disconnects from the server if a connection is active
        /// </summary>
        /// <returns></returns>
        public async Task Disconnect()
        {
            if (!initialized || !Connected || gameHub == null) return;

            await DeleteUser(new UserMessage(ClientId, user));
            await gameHub.StopAsync();

            Connected = false;
        }

        /// <summary>
        /// Makes a request to the server to transition screens
        /// </summary>
        /// <param name="screenName">The name of the screen to transition to</param>
        public async Task RequestScreenTransition(string screenName)
        {
            await TryInvoke(nameof(GameHub.MoveToScreen), new ScreenMessage(screenName));
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
        /// Requests that the server resend each joined member (which will include
        /// this client). Usually called when first joining a server.
        /// </summary>
        public async Task ReckonUsers()
        {
            await TryInvoke(nameof(GameHub.ReckonUsers));
        }
        /// <summary>
        /// Updates the provided user. The server will register the user if they
        /// are new, otherwise it will update the username associated with the
        /// connected client if it has changed.
        /// </summary>
        /// <param name="message">The UserMessage with user details</param>
        public async Task UpdateUser(UserMessage message)
        {
            await TryInvoke<UserMessage>(nameof(GameHub.UpdateUser), message);
        }
        /// <summary>
        /// Sends a request to delete the provided user.
        /// </summary>
        /// <param name="message">The user to delete</param>
        public async Task DeleteUser(UserMessage message)
        {
            await TryInvoke<UserMessage>(nameof(GameHub.DeleteUser), message);
        }

        /// <summary>
        /// Sends a chat message to all players on the server.
        /// </summary>
        /// <param name="message">The message string</param>
        public async Task SendChat(string chat)
        {
            var message = new ChatMessage(
                ClientId,
                user,
                chat);
            await TryInvoke<ChatMessage>(nameof(GameHub.SendChat), message);
        }
        /// <summary>
        /// Requests that the server resend all chat messages. Usually called
        /// when first joining or when a complete refresh is needed.
        /// </summary>
        public async Task RequestAllChats()
        {
            await TryInvoke(nameof(GameHub.RequestAllChats));
        }
        /// <summary>
        /// Requests that the server delete chat message history. Useful when starting a new
        /// lobby or new game to clear old chat messages.
        /// </summary>
        public async Task DeleteAllChats()
        {
            await TryInvoke(nameof(GameHub.DeleteAllChats));
        }

        /// <summary>
        /// Sends a request for an entity to be created, which will
        /// bounce back to the IGameClient when the server echoes it
        /// back to all clients
        /// </summary>
        /// <param name="message">The entity creation message</param>
        public async Task RequestCreateEntity(EntityMessage message)
        {
            await TryInvoke(nameof(GameHub.CreateEntity), message);
        }
        /// <summary>
        /// Sends a request to update an entity, which will bounce back
        /// to the IGameClient when the server echoes it to all clients
        /// </summary>
        /// <param name="message">The entity to update</param>
        public async Task RequestUpdateEntity(EntityMessage message)
        {
            await TryInvoke(nameof(GameHub.UpdateEntity), message);
        }
        /// <summary>
        /// Sends a request to delete an entity, which will bounce back to the
        /// IGameClient when the server echoes the message to all clients
        /// </summary>
        /// <param name="message">The entity to delete</param>
        public async Task RequestDeleteEntity(EntityMessage message)
        {
            await TryInvoke(nameof(GameHub.UpdateEntity), message);
        }
        /// <summary>
        /// Sends a request to the server to provide a reckoning message that
        /// contains all entities
        /// </summary>
        public async Task ReckonEntities()
        {
            await TryInvoke(nameof(GameHub.ReckonAllEntities));
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

            gameHub.Closed += (exception) => gameClient.FailConnection(exception);

            gameHub.On<ScreenMessage>(nameof(IGameClient.MoveToScreen),
                message => gameClient.MoveToScreen(message));

            gameHub.On<UserMessage>(nameof(IGameClient.RegisterUser),
                message => gameClient.RegisterUser(message));
            gameHub.On<UserMessage>(nameof(IGameClient.DeleteUser),
                message => gameClient.DeleteUser(message));
            gameHub.On<List<UserMessage>>(nameof(IGameClient.ReckonUsers),
                message => gameClient.ReckonUsers(message));

            gameHub.On<ChatMessage>(nameof(IGameClient.ReceiveChat),
                message => gameClient.ReceiveChat(message));
            gameHub.On(nameof(IGameClient.DeleteAllChats),
                () => gameClient.DeleteAllChats());

            gameHub.On<EntityMessage>(nameof(IGameClient.CreateEntity),
                message => gameClient.CreateEntity(message));
            gameHub.On<EntityMessage>(nameof(IGameClient.UpdateEntity),
                message => gameClient.UpdateEntity(message));
            gameHub.On<EntityMessage>(nameof(IGameClient.DeleteEntity),
                message => gameClient.DeleteEntity(message));
            gameHub.On<List<EntityMessage>>(nameof(IGameClient.ReckonEntities),
                message => gameClient.ReckonEntities(message));
            
        }
    }
}
