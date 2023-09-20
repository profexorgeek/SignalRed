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
        /// Called when a new message is received from the server
        /// </summary>
        /// <param name="message">The message</param>
        Task ReceiveMessage(ChatMessage message);

        /// <summary>
        /// Called when an entity is updated (created, updated, or deleted)
        /// </summary>
        /// <param name="message">The entity message</param>
        Task UpdateEntity(EntityMessage message);
    }
}
