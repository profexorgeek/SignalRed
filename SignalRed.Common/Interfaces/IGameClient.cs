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
        Task MoveToScreen(string screenName);


        /// <summary>
        /// Called when a new user joins the server, or an existing user changes
        /// their name
        /// </summary>
        /// <param name="username">The new username</param>
        Task RegisterUser(string id, string username);

        /// <summary>
        /// Called when first joining the server to get the list
        /// of other members.
        /// </summary>
        /// <param name="users">A KVP containing id:username pairs</param>
        Task ReceiveAllUsers(Dictionary<string, string> users);



        /// <summary>
        /// Called when a new message is received from the server
        /// </summary>
        /// <param name="message">The message</param>
        Task ReceiveMessage(ChatMessage message);

        /// <summary>
        /// Called when all existing messages from this session are received
        /// from the server.
        /// </summary>
        /// <param name="messages">The messages received from the server</param>
        Task ReceiveAllMessages(List<ChatMessage> messages);

        /// <summary>
        /// Called when an entity update is received.
        /// </summary>
        /// <typeparam name="T">The entity message type</typeparam>
        /// <param name="entityToUpdate">The entity state data to update</param>
        Task UpdateEntity(string type, object entityData);
    }
}
