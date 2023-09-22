using SignalRed.Common.Interfaces;
using System.Text.Json;

namespace SignalRed.Common.Messages
{
    /// <summary>
    /// A message designed to carry a serialized payload
    /// with a specified type, allowing it to be deserialized
    /// into a static type when it is received by a client.
    /// 
    /// Usually used for syncing the generic concept of game
    /// entities across the network.
    /// </summary>
    public class EntityStateMessage : INetworkMessage
    {
        public string SenderClientId { get; set; }
        public string SenderConnectionId { get; set; }

        /// <summary>
        /// The target's unique identifier if the target
        /// is an entity.
        /// 
        /// Messages that don't have a unique identifier will not be saved by the
        /// server for reckoning
        /// </summary>
        public string EntityId { get; set; }

        /// <summary>
        /// The unique ID (client ID, not connection ID) of the client that created this entity
        /// </summary>
        public string OwnerId { get; set; }

        /// <summary>
        /// The full type name of the payload. This should usually
        /// NOT be set directly, instead call SetPayload to set the
        /// payload and the type at once.
        /// </summary>
        public string? StateType { get; set; }

        /// <summary>
        ///  The payload, which is usually some representation of
        ///  an entity's state, serialized into a string. This should
        ///  usually NOT be set directly, instead call SetPayload to
        ///  set the payload and type at once.
        /// </summary>
        public string? SerializedState { get; set; }

        
        public EntityStateMessage(string senderId, string connectionId, string targetId)
        {
            SenderClientId = senderId;
            SenderConnectionId = connectionId;
            EntityId = targetId;
        }
        
        /// <summary>
        /// Sets the Payload and PayloadType property by serializing 
        /// the provided state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="state"></param>
        public void SetState<T>(T state)
        {
            SerializedState = JsonSerializer.Serialize(state);
            StateType = typeof(T).FullName;
        }

        /// <summary>
        /// Attempts to deserialize the payload into the
        /// target type T
        /// </summary>
        /// <typeparam name="T">The payload type</typeparam>
        /// <returns>A desrialized object or an exception</returns>
        /// <exception cref="Exception">An exception thrown when the provided type T 
        /// does not match PayloadType</exception>
        public T GetState<T>()
        {
            if(typeof(T).FullName == StateType)
            {
                return JsonSerializer.Deserialize<T>(SerializedState);
            }
            else
            {
                throw new Exception($"Tried to deserialize payload of type {StateType} as type {typeof(T).FullName}");
            }
        }

        /// <summary>
        /// Gets a string representation of this message, usually
        /// used for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"{EntityId}({StateType})";
        }
    }
}
