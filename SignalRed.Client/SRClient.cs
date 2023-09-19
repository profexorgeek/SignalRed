using Microsoft.AspNetCore.SignalR.Client;
using SignalRed.Common.Messages;

namespace SignalRed.Client
{
    public class SRClient
    {
        private static SRClient instance;
        private HubConnection? gameHub;
        private bool initialized = false;

        public static SRClient Instance => instance ?? (instance = new SRClient());
        public string? ClientId => gameHub?.ConnectionId;
        public bool Connected { get; private set; } = false;

        /// <summary>
        /// Initializes the client service
        /// </summary>
        public void Initialize()
        {
            initialized = true;
        }

        /// <summary>
        /// Connects to a server at the provided URL, which should
        /// include the protocol (http) and port number
        /// </summary>
        /// <param name="url">The url to connect to, including protocol and port number</param>
        public async void Connect(Uri url)
        {
            // first, break any existing connections
            Disconnect();


            // now build our hub connection
            var gameHubUrl = new Uri(url, "game");
            gameHub = new HubConnectionBuilder()
                .WithUrl(gameHubUrl)
                .Build();

            // register communication paths
            gameHub.On<ChatMessage>(nameof(IGameClient.ReceiveMessage), message =>
            {
                HandleChat(message);
            });

            gameHub.On<List<ChatMessage>>(nameof(IGameClient.ReceiveAllMessages), messages =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    HandleChat(messages[i]);
                }
            });

            // manage connection failures
            gameHub.Closed += (exception) =>
            {
                Console.WriteLine("Connection closed with error: " + exception?.Message);
                return Task.CompletedTask;
            };

            // form the connection
            await gameHub.StartAsync();

            // TODO: register username

            // after connecting, we fetch all past messages
            await gameHub.InvokeAsync(nameof(IGameClient.ReceiveAllMessages));

            Connected = true;
        }

        /// <summary>
        /// Disconnects from the server if a connection is active
        /// </summary>
        /// <returns></returns>
        public async Task Disconnect()
        {
            if (gameHub != null)
            {
                await gameHub.StopAsync();
            }

            Connected = false;
        }

        /// <summary>
        /// Sends a chat message
        /// </summary>
        /// <param name="chatMessage">The message to send</param>
        /// <returns>A task representing the message send status</returns>
        public async Task SendMessage(string chatMessage)
        {
            // EARLY OUT: swallow message if not connected to anything
            if (!Connected || gameHub == null) return;

            await gameHub.InvokeAsync(nameof(IGameClient.ReceiveMessage), new ChatMessage(
                gameHub.ConnectionId,
                chatMessage
                ));
        }


        /// <summary>
        /// Handles an incoming chat message from the server
        /// 
        /// TODO: this should probably invoke some sort of event the
        /// client can listen for
        /// </summary>
        /// <param name="message">The incoming message</param>
        void HandleChat(ChatMessage message)
        {
            Console.WriteLine(message);
        }
    }
}
