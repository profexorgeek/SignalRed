using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRed.Client
{
    // a demo representation of a game entity's state
    struct ExampleEntityState : INetworkEntityState
    {
        public string OwnerId { get; set; }
        public string EntityId { get; set; }
        public UpdateType UpdateType { get; set; }
        public string EntityType => this.GetType().FullName;
        public float CurrentHealth { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public override string ToString()
        {
            return $"{EntityId}: ({X},{Y}) +{CurrentHealth}";
        }
    }

    class ConsoleGameClient : IGameClient
    {
        string currentScreen = "Title";
        Dictionary<string, ExampleEntityState> entities = new Dictionary<string, ExampleEntityState> ();
        Dictionary<string, string> users = new Dictionary<string, string> ();

        public async Task Test()
        {
            var rand = new Random();
            var user = $"user#{rand.Next(0, 999)}";
            var uri = new Uri("http://localhost:5000");

            SRClient.Instance.Initialize(this);
            await SRClient.Instance.ConnectAsync(uri, user);

            // if we have a null screen, we should request a screen transition
            // this just tests that the first client to join sets the screen and
            // subsequent clients should receive the screen
            if(currentScreen == null)
            {
                await SRClient.Instance.RequestScreenTransition("Lobby");
            }

            // send a chat
            await SRClient.Instance.SendMessageAsync($"Hi everyone, this is {user}!");

            // add some randomized entities
            for (var i = 0; i < rand.Next(1, 5); i++)
            {
                var id = SRClient.Instance.GetUniqueEntityId();
                var entity = new ExampleEntityState()
                {
                    EntityId = id,
                    OwnerId = SRClient.Instance.ClientId,
                    X = rand.Next(-100, 100),
                    Y = rand.Next(-100, 100),
                    CurrentHealth = rand.Next(0, 10),
                    UpdateType = UpdateType.Create,
                };

                await SRClient.Instance.UpdateEntityAsync(entity);
            }

            // update an entity we own
            var entityToUpdate = entities.Where(e => e.Value.OwnerId == SRClient.Instance.ClientId).First().Value;
            entityToUpdate.CurrentHealth = rand.Next(0, 10);
            entityToUpdate.UpdateType = UpdateType.Update;
            await SRClient.Instance.UpdateEntityAsync(entityToUpdate);

            // delete an entity we own
            var entityToDelete = entities.Where(e => e.Value.OwnerId == SRClient.Instance.ClientId).Last().Value;
            entityToDelete.UpdateType = UpdateType.Delete;
            await SRClient.Instance.UpdateEntityAsync(entityToDelete);

        }

        public Task FailConnection(Exception exception)
        {
            Console.WriteLine("Connection failed: " + exception.Message);
            return Task.CompletedTask;
        }
        public Task MoveToScreen(string screenName)
        {
            Console.WriteLine($"Moving to screen {screenName}");
            currentScreen = screenName;
            return Task.CompletedTask;
        }

        public Task RegisterUser(string id, string username)
        {
            Console.WriteLine($"{username} has joined the chat!");
            if (users.ContainsKey(id))
            {
                users[id] = username;
            }
            else
            {
                users.Add(id, username);
            }
            return Task.CompletedTask;
        }
        public Task ReceiveAllUsers(Dictionary<string, string> _users)
        {
            users.Clear();
            foreach(var kvp in _users)
            {
                RegisterUser(kvp.Key, kvp.Value);
            }
            return Task.CompletedTask;
        }

        public Task UpdateEntity(string typeString, object? entityToUpdate)
        {
            if(typeString != typeof(ExampleEntityState).FullName)
            {
                throw new Exception($"Got entity update of unknown type: {typeString}");
            }

            ExampleEntityState typedEntity = (ExampleEntityState)entityToUpdate;

            var id = typedEntity.EntityId;
            var msgType = typedEntity.UpdateType;

            switch (msgType)
            {
                case UpdateType.Unknown:
                    // NOOP: we shouldn't be here
                    break;
                case UpdateType.Create:
                    if (!entities.ContainsKey(id))
                    {
                        entities.Add(id, typedEntity);
                    }
                    else
                    {
                        entities[id] = typedEntity;
                    }
                    Console.WriteLine("Created entity: " + typedEntity);
                    break;
                case UpdateType.Update:
                    if (!entities.ContainsKey(id))
                    {
                        entities.Add(id, typedEntity);
                    }
                    entities[id] = typedEntity;
                    Console.WriteLine("Updated entity: " + typedEntity);
                    break;
                case UpdateType.Delete:
                    if (entities.ContainsKey(id))
                    {
                        entities.Remove(id);
                        Console.WriteLine("Deleted entity: " + typedEntity);
                    }
                    break;
            }
            return Task.CompletedTask;
        }

        public Task ReceiveAllMessages(List<ChatMessage> message)
        {
            for (var i = 0; i < message.Count; i++)
            {
                ReceiveMessage(message[i]);
            }
            return Task.CompletedTask;
        }
        public Task ReceiveMessage(ChatMessage message)
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        }
    }
}
