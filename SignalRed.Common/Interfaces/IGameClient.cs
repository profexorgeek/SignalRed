using SignalRed.Common.Messages;
using System.Text.Json;

namespace SignalRed.Common.Interfaces
{
    /// <summary>
    /// This interface is not implemented. It simply defines the contract of things
    /// a game client can handle. The SignalRedClient lists for the server to call
    /// these methods and maps them to events that can be handled in the game client.
    /// </summary>
    public interface IGameClient
    {
        /// <summary>
        /// Called when a connection fails
        /// </summary>
        /// <param name="reason">The reason the connection failed</param>
        Task FailConnection(string reason);

        /// <summary>
        /// Called when a message is received that clients should migrate to a
        /// new screen.
        /// </summary>
        /// <param name="screenName">The target screen to move to</param>
        Task MoveToScreen(ScreenMessage message);

        /// <summary>
        /// Receives the current server time
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        Task ReceiveServerTime(string requestId, double milliseconds);

        /// <summary>
        /// Called when a new user joins the server
        /// </summary>
        /// <param name="username">The new username</param>
        Task CreateConnection(Messages.NetworkMessage message);

        /// <summary>
        /// Called by the server when the client should reckon its
        /// list of connections with the master list from the server.
        /// </summary>
        /// <param name="message">A list of connections</param>
        Task ReckonConnections(List<Messages.NetworkMessage> message);

        /// <summary>
        /// Called by the server when an existing connection is deleted. Usually called before
        /// disconnecting to gracefully disconnect.
        /// </summary>
        /// <param name="message">The connection to remove</param>
        Task DeleteConnection(Messages.NetworkMessage message);



        /// <summary>
        /// Receives a payload message that should represent a new
        /// entity.
        /// </summary>
        /// <param name="message">The payload message, usually containing a new entity state</param>
        Task CreateEntity(EntityStateMessage message);

        /// <summary>
        /// Receives a payload message that should represent an
        /// entity update.
        /// </summary>
        /// <param name="message">The payload, usually containing an updated entity state</param>
        Task UpdateEntity(EntityStateMessage message);

        /// <summary>
        /// Receives a payload message that should represent an
        /// entity to be deleted.
        /// </summary>
        /// <param name="message">The payload, usually containing the entity information to be deleted</param>
        /// <returns></returns>
        Task DeleteEntity(EntityStateMessage message);

        /// <summary>
        /// Receives all payloads that the server is tracking. This should be used to
        /// sync a local client with the server.
        /// </summary>
        /// <param name="message">A list of payloads representing current entity states</param>
        Task ReckonEntities(List<EntityStateMessage> message);



        /// <summary>
        /// Called when client should receive a generic message
        /// </summary>
        /// <param name="message">The generic message, a key,value pair</param>
        Task ReceiveGenericMessage(GenericMessage message);
    }
}
