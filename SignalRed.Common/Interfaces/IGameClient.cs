using SignalRed.Common.Messages;
using System.Text.Json;

namespace SignalRed.Common.Interfaces
{
    public interface IGameClient
    {
        /// <summary>
        /// Called when a connection fails with an exception.
        /// Allows the game client to react to the exception.
        /// </summary>
        /// <param name="exception">The exception that caused the failed connection.</param>
        Task FailConnection(Exception exception);

        /// <summary>
        /// Called when a message is received that clients should migrate to a
        /// new screen.
        /// </summary>
        /// <param name="screenName">The target screen to move to</param>
        Task MoveToScreen(ScreenMessage message);

        /// <summary>
        /// Called when a new user joins the server or a user changes their name
        /// </summary>
        /// <param name="username">The new username</param>
        Task RegisterUser(UserMessage message);
        /// <summary>
        /// Called by the server when an existing user is deleted. Usually called before
        /// disconnecting to gracefully disconnect.
        /// </summary>
        /// <param name="message">The user to remove</param>
        Task DeleteUser(UserMessage message);
        /// <summary>
        /// Called by the server when the client has asked for a list of
        /// all current users.
        /// </summary>
        /// <param name="users">The server's list of users</param>
        Task ReckonUsers(List<UserMessage> users);


        /// <summary>
        /// Called when a new message is received from the server
        /// </summary>
        /// <param name="message">The message</param>
        Task ReceiveChat(ChatMessage message);
        /// <summary>
        /// Called when the server notifies client that the chat queue should be cleared
        /// </summary>
        Task DeleteAllChats();

        /// <summary>
        /// Called by the server when an entity is created
        /// </summary>
        /// <param name="message">The entity to create</param>
        Task CreateEntity(EntityMessage message);
        /// <summary>
        /// Called by the server when an entity is updated. If this
        /// is a reckoning message, the client should force the incoming state
        /// to ensure game consistency.
        /// </summary>
        /// <param name="message">The entity to update</param>
        /// <param name="reckoning">If this is a reckoning message</param>
        Task UpdateEntity(EntityMessage message);
        /// <summary>
        /// Called by the server when an entity is deleted
        /// </summary>
        /// <param name="message">The entity to delete</param>
        /// <returns></returns>
        Task DeleteEntity(EntityMessage message);
        /// <summary>
        /// Called when client should force their local state to match
        /// the state provided by the server
        /// </summary>
        /// <param name="entities"></param>
        Task ReckonEntities(List<EntityMessage> entities);
    }
}
