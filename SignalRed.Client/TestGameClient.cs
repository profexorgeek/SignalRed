using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;

namespace SignalRed.Client
{    
    public class EntityState
    {
        public string Name { get; set; } = "None";
        public float X { get; set; }
        public float Y { get; set; }
        public float CurrentHealth { get; set; }

        public override string ToString()
        {
            return $"{Name} ({X},{Y}) +{CurrentHealth}";
        }
    }

    public class TestGameClient : IGameClient
    {
        public const string ServerUrl = "http://localhost:5000";

        string currentScreen = "None";
        Random rand = new Random();
        string username = "User";

        public TestGameClient() {}

        public async Task Test()
        {
            SRClient.Instance.Initialize(this);

            username = $"user#{rand.Next(1, 999)}";

            WriteMessage("=== Connecting to Server ===", ConsoleColor.Cyan);
            await SRClient.Instance.Connect(ServerUrl, username);

            WriteMessage("=== Requesting Users ===", ConsoleColor.Cyan);
            await SRClient.Instance.ReckonUsers();

            WriteMessage("=== Getting Chats ===", ConsoleColor.Cyan);
            await SRClient.Instance.RequestAllChats();

            WriteMessage("=== Getting Current Screen ===", ConsoleColor.Cyan);
            await SRClient.Instance.RequestCurrentScreen();

            WriteMessage("=== Asking Server to Transition to New Screen ===", ConsoleColor.Cyan);
            var screen = $"Screen#{rand.Next(1, 999)}";
            await SRClient.Instance.RequestScreenTransition(screen);

            WriteMessage("=== Updating User ===", ConsoleColor.Cyan);
            username = $"user#{rand.Next(1, 999)}";
            await SRClient.Instance.UpdateUser(
                new UserMessage(SRClient.Instance.ClientId, username));

            var chat = "Hello friends!";
            WriteMessage($"=== Sending Test Chat: {chat} ===", ConsoleColor.Cyan);
            await SRClient.Instance.SendChat(chat);

            WriteMessage("=== Sending Entity Creation ===", ConsoleColor.Cyan);
            var entity = new EntityState()
            {
                Name = $"entity#{rand.Next(0, int.MaxValue)}",
                X = rand.Next(-500, 500),
                Y = rand.Next(-500, 500),
                CurrentHealth = rand.Next(1, 100),
            };
            var msg = new EntityMessage()
            {
                Id = SRClient.Instance.GetUniqueEntityId(),
                Owner = SRClient.Instance.ClientId,
            };
            msg.SetPayload(entity);
            await SRClient.Instance.RequestCreateEntity(msg);

            WriteMessage("=== Sending Entity Update ===", ConsoleColor.Cyan);
            entity.Name += "--UPDATED";
            msg.SetPayload(entity);
            await SRClient.Instance.RequestUpdateEntity(msg);

            WriteMessage("=== Reckoning Entities ===", ConsoleColor.Cyan);
            await SRClient.Instance.ReckonEntities();

            //WriteMessage("Attempting to delete owned entity...");
            //await SRClient.Instance.RequestDeleteEntity(msg);

            //WriteMessage("Attempting to disconnect...");
            //await SRClient.Instance.Disconnect();
        }

        public Task FailConnection(Exception exception)
        {
            WriteMessage("Connection failed!", ConsoleColor.Red);
            return Task.CompletedTask;
        }
        public Task MoveToScreen(ScreenMessage message)
        {
            currentScreen = message.NewScreen;
            WriteMessage($"Moved to screen {currentScreen}", ConsoleColor.Green);
            return Task.CompletedTask;
        }


        public Task RegisterUser(UserMessage message)
        {
            WriteMessage($"{message.UserName} has joined the server.", ConsoleColor.Green);
            return Task.CompletedTask;
        }
        public Task DeleteUser(UserMessage message)
        {
            WriteMessage($"{message.UserName} has left the server.", ConsoleColor.Green);
            return Task.CompletedTask;
        }
        public Task ReckonUsers(List<UserMessage> users)
        {
            WriteMessage("Received user reckoning:", ConsoleColor.Green);
            foreach(var user in users)
            {
                WriteMessage($"- {user.UserName}", ConsoleColor.Yellow);
            }
            WriteMessage("User reckoning complete.", ConsoleColor.Green);
            return Task.CompletedTask;
        }

        public Task ReceiveChat(ChatMessage message)
        {
            WriteMessage(message.ToString(), ConsoleColor.Green);
            return Task.CompletedTask;
        }
        public Task DeleteAllChats()
        {
            WriteMessage("Clearing chat log...", ConsoleColor.Green);
            return Task.CompletedTask;
        }


        public Task CreateEntity(EntityMessage message)
        {
            var state = message.GetPayload<EntityState>();
            WriteMessage($"Received entity CREATE request {state.Name}", ConsoleColor.Green);
            return Task.CompletedTask;
        }
        public Task UpdateEntity(EntityMessage message)
        {
            var state = message.GetPayload<EntityState>();
            WriteMessage($"Received entity UPDATE request {state.Name}", ConsoleColor.Green);
            return Task.CompletedTask;
        }
        public Task DeleteEntity(EntityMessage message)
        {
            var state = message.GetPayload<EntityState>();
            WriteMessage($"Received entity DELETE request {state.Name}", ConsoleColor.Green);
            return Task.CompletedTask;
        }
        public Task ReckonEntities(List<EntityMessage> entities)
        {
            WriteMessage("Received entity reckoning:", ConsoleColor.Green);
            foreach(var message in entities)
            {
                var state = message.GetPayload<EntityState>();
                WriteMessage($"- {state.Name}", ConsoleColor.Yellow);
            }
            return Task.CompletedTask;
        }

        void WriteMessage(string msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
        }
    }
}
