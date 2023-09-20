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

            Console.WriteLine("=== Connecting to Server ===");
            await SRClient.Instance.Connect(ServerUrl, username);

            Console.WriteLine("=== Requesting Users ===");
            await SRClient.Instance.ReckonUsers();

            Console.WriteLine("=== Getting Chats ===");
            await SRClient.Instance.RequestAllChats();

            Console.WriteLine("=== Getting Current Screen ===");
            await SRClient.Instance.RequestCurrentScreen();

            Console.WriteLine("=== Asking Server to Transition to New Screen ===");
            var screen = $"Screen#{rand.Next(1, 999)}";
            await SRClient.Instance.RequestScreenTransition(screen);

            Console.WriteLine("=== Updating User ===");
            username = $"user#{rand.Next(1, 999)}";
            await SRClient.Instance.UpdateUser(
                new UserMessage(SRClient.Instance.ClientId, username));

            var chat = "Hello friends!";
            Console.WriteLine($"=== Sending Test Chat: {chat} ===");
            await SRClient.Instance.SendChat(chat);

            Console.WriteLine("=== Sending Entity Creation ===");
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

            Console.WriteLine("=== Sending Entity Update ===");
            entity.Name += "--UPDATED";
            msg.SetPayload(entity);
            await SRClient.Instance.RequestUpdateEntity(msg);

            Console.WriteLine("=== Reckoning Entities ===");
            await SRClient.Instance.ReckonEntities();

            //Console.WriteLine("Attempting to delete owned entity...");
            //await SRClient.Instance.RequestDeleteEntity(msg);

            //Console.WriteLine("Attempting to disconnect...");
            //await SRClient.Instance.Disconnect();
        }

        public Task FailConnection(Exception exception)
        {
            Console.WriteLine("Connection failed!");
            return Task.CompletedTask;
        }
        public Task MoveToScreen(ScreenMessage message)
        {
            currentScreen = message.NewScreen;
            Console.WriteLine($"Moved to screen {currentScreen}");
            return Task.CompletedTask;
        }


        public Task RegisterUser(UserMessage message)
        {
            Console.WriteLine($"{message.UserName} has joined the server.");
            return Task.CompletedTask;
        }
        public Task DeleteUser(UserMessage message)
        {
            Console.WriteLine($"{message.UserName} has left the server.");
            return Task.CompletedTask;
        }
        public Task ReckonUsers(List<UserMessage> users)
        {
            Console.WriteLine("Received user reckoning:");
            foreach(var user in users)
            {
                Console.WriteLine($"- {user.UserName}");
            }
            Console.WriteLine("User reckoning complete.");
            return Task.CompletedTask;
        }

        public Task ReceiveChat(ChatMessage message)
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        }
        public Task DeleteAllChats()
        {
            Console.WriteLine("Clearing chat log...");
            return Task.CompletedTask;
        }


        public Task CreateEntity(EntityMessage message)
        {
            var state = message.GetPayload<EntityState>();
            Console.WriteLine($"Received entity CREATE request {state.Name}");
            return Task.CompletedTask;
        }
        public Task UpdateEntity(EntityMessage message)
        {
            var state = message.GetPayload<EntityState>();
            Console.WriteLine($"Received entity UPDATE request {state.Name}");
            return Task.CompletedTask;
        }
        public Task DeleteEntity(EntityMessage message)
        {
            var state = message.GetPayload<EntityState>();
            Console.WriteLine($"Received entity DELETE request {state.Name}");
            return Task.CompletedTask;
        }
        public Task ReckonEntities(List<EntityMessage> entities)
        {
            Console.WriteLine("Received entity reckoning:");
            foreach(var message in entities)
            {
                var state = message.GetPayload<EntityState>();
                Console.WriteLine($"- {state.Name}");
            }
            return Task.CompletedTask;
        }

        
    }
}
