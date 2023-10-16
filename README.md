# SignalRed

**NOTE: Early in Development, expect frequent breaks and lots of bugs - Contributors and bug reports welcome!**

SignalRed is a multiplayer networking library that uses Microsoft's SignalR as a transport layer. It was written to make it easier to build indie games in C# with simple, opinionated networking patterns.

## What/Who Should Use SignalRed

If you want a simple networking solution in C#, with the ability to scale your server in the cloud, SignalRed is a good fit. Since it uses SignalR, the server is platform agnostic and it's possible to build clients across mobile, web, and desktop that all interact with the same server.

SignalRed offers a simple server that primarily bounces messages across clients. Currently this server is extremely simple and is just designed to automatically relay a handful of base message types. However, you can implement your own server that adds cheat prevention, server-side simulation, or whatever else your game requires!

It also has a Singleton `SignalRedClient` class that makes it easy to send and fetch messages in a few opinionated types. This allows an indie game developer to start writing network entity management code on day 1 instead of focusing on transport types, sockets, server config, etc.

SignalRed is good for small indie games that are realtime or turn based. It probably isn't a good fit for any game that requires high-precision timing (fighting games), rollback netcode (fighting games), partitioning (mmo), or other scenarios where deep control over transport layers and performance is critical.

* SignalRed is in C# and uses a scalable Microsoft stack
* The `SignalRed.Server` instance can be hosted in the cloud as an Azure instance so you don't have to worry about things like NAT punchthrough
* Only one transport type: everything in SignalR is TCP
* SignalRed [currently] has no protection from cheating. Since the server is a simple message relay, cheat prevention is the responsibility of your client code.
* SignalRed [currently] has no concept of authority (host vs guest)
* SignalRed [currently] uses the default `System.Text.Json` serialization and doesn't offer a way to customize this
* SignalRed has an example game called [SignalRed Tanks](https://github.com/profexorgeek/SignalRedTanks) that is used to test and expand its features

## How It Works

If you like getting straight into code, please see the example game [SignalRed Tanks](https://github.com/profexorgeek/SignalRedTanks).

## SignalRed Message Types

SignalRed revolves around the concept of exchanging messages. Messages all derive from `SignalRed.Common.Messages.NetworkMessage` and guarantee that messages will have:

* SenderClientId - the id of the client that sent the message, which should not change if a client disconnects and reconnects.
* SenderConnectionId - the id of the client's _connection_, which can change as the client connects and disconnects.
* SendTime - the server time when the message was sent
* DeltaSeconds - the elapsed time in seconds between when the message was sent and the current time

These properties allow each client to know exactly who sent a message and how long ago it was sent. The base messsage type is generally not directly used. Instead, SignalRed provides these higher-order message types:

* `ScreenMessage` - a message sent when clients should move to a specific screen, view, or level such as "MainMenu".
* `GenericMessage` - a message that contains a key-value pair. These are useful for one-off messages with arbitrary information.
* `EntityStateMessage` - these messages do the heavy lifting, managing the state of `ISignalRedEntity` objects across the network. Entity messages include a `SignalRedMessageType` which can be
  - Create: sent when clients should create an entity
  - Update: sent when clients should update an entity
  - Reckon: sent when clients should force itself to synchronize with an incoming state from some authoritative source
  - Delete: sent when clientds should delete an entity

## ISignalRedEntity<T>

It is assumed that the message types above can handle any type of communication needed by your game. In general, most of your game objects should be abstractly represented by the concept of an `Entity`. That entity has a `State` (which is a 1:1 relationship), and the network's job is to manage synchronizing those entity states across all connected clients. The `ISignalRedEntity<T>` interface is a contract that guarantees your game entities can be synchronized across the network.

The `T` argument in the interface is the `type` that represents that entities state across the network. For example, you might have a `Tank` entity class in your game whose state is represented by a `TankNetworkState` object. Your class definition might look like: `public class Tank : ISignalRedEntity<TankNetworkState>`

The `ISignalRedEntity<T>` requires your entity to have these things:

- OwnerClientId Property - defines which connected client owns an entity
- EntityId Property - a unique identifier across the network. This is automatically created by the `SignalRedClient` when creating an entity using a combination of its ClientId and an incrementing counter.
- `ApplyCreationState` method - provides a state of type `T` that should be used to set up a new entity
- `ApplyUpdateState` method - provides a state of type `T` that should be used to update an entity
- `Destroy` method - provides a state of type `T` and should destroy the entity and remove it from the game
- `GetState` method - returns a state of type `T` that represents this entity

The methods in the interface also include a `deltaSeconds` parameter that should indicate how many seconds have passed since the message was sent. This allows for physics interpolation. Likewise, the `Destroy` method is passed a state and a time in case there is significant latency. This allows entities to play effects in the right place and deal with latency.

Finally, it's worth noting that all incoming states are optionally applied! You may choose to have a locally-controlled entity ignore all incoming network states and enforce its own state as the authority across the network. Additionally, it is the developer's choice when and how often a networked entity should broadcast its own state to other clients.

## SignalRedClient

The `SignalRedClient` class is a Singleton that provides everything your game client needs to communicate with other clients through the network. The `SignalRedClient` is accessed through the static instance property, for example: `SignalRedClient.Instance.SomeMethod()`.

By default, SignalR is heavily event-driven. This is problematic in most game engines that rely on the concept of a gameloop to do the work each frame and event-driven network messages cause threading problems! A major thing the `SignalRedClient` does is collect all messages in a thread-safe collection, and make them available for retrieval on the main thread of your game loop.

The typical lifecycle of the `SignalRedClient` is as follows:

1. Initialize in your core `Game` or `Program` class: `SignalRedClient.Instance.Initialize()`. This will assign a client ID, prepare the client for communicating with a network, and allows you to provide an optional `Action` in the case of connection failure.
2. Connect to a server using `Connect(string url, Action? onConnectedCallback = null)`
3. Fetch messages each frame in your game loop:
  - Get the latest screen message using `GetCurrentScreen()`
  - Get any `GenericMessage`s that arrived since the last frame using `GetGenericMessages()`
  - Get any `EntityMessage`s that arrived since the last frame using `GetEntityMessages()`
3. Send messages in your game loop:
  - Send Create, Update, or Delete entity messages using methods such as `UpdateEntity<T>(ISignalRedEntity<T> entity)`
  - Send Generic messages using `CreateGenericMessage(string key, string value)`
  - Request screen transitions using `RequestScreenTransition(string screenName)`
4. Disconnect from a server using `Disconnect(Action? onDisconnectedCallback = null)`

Each frame, the game client may get multiple entity messages representing different actions the client should take to synchronize entities across the network. It is recommended that you fetch all entity messages and handle them by type every frame. For example, you might perform this logic each frame in your game loop:

```csharp
// get all entity messages since last frame
var entityMessages = SignalRedClient.Instance.GetEntityMessages();
for(var i = 0; i < entityMessages.Count; i++)
{
    // each message is a tuple including the message and it's type
    var message = entityMessages[i].Item1;
    var type = entityMessages[i].Item2;

    // handle each message type
    switch(type)
    {
        case SignalRedMessageType.Create:
            CreateEntity(message);
            break;
        case SignalRedMessageType.Update:
            UpdateEntity(message);
            break;
        case SignalRedMessageType.Reckon:
            UpdateEntity(message, true);
            break;
        case SignalRedMessageType.Delete:
            DeleteEntity(message);
            break;
        default:
            throw new Exception($"Unexpected entity message type: {type} {message.StateType}");
    }
}
```

When handling an entity message, you must deserialize the state payload and apply it to the entity. Here's a pseudocode example of how you might handle an incoming entity creation message:

```csharp
void CreateEntity(EntityStateMessage message)
{
    // Check the message type against known types, there may be
    // many of these in your game!
    if (message.StateType == typeof(TankNetworkState).FullName)
    {
        // now we know the type, deserialize the state
        var state = message.GetState<TankNetworkState>();

        // make sure we don't already have this entity using some way
        // of checking our local list of entities
        if (CheckIfEntityExists(message.EntityId) == false)
        {
            // create a new game entity
            var tank = TankBaseFactory.CreateNew(state.X, state.Y);

            // set the owner and entity ID and apply the creation state
            tank.OwnerClientId = message.OwnerClientId;
            tank.EntityId = message.EntityId;
            tank.ApplyCreationState(state, message.DeltaSeconds);
        }
    }
}
```

## Summary

SignalRed focuses on a simple pattern of defining entities that adhere to an interface, and using CRUD-like patterns to manage the lifecycle of these entities across the network.

Your questions, comments, and contributions are welcome. Please file issues if you encounter bugs or have problems with the patterns!




