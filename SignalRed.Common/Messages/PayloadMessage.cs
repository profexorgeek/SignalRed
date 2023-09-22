using SignalRed.Common.Interfaces;
using System.Text.Json;

namespace SignalRed.Common.Messages
{
    /// <summary>
    /// A message designed to carry a serialized payload
    /// with a specified type, allowing it to be deserialized
    /// into a static type when it is received by a client.
    /// 
    /// Usually used for syncing entities across the network.
    /// </summary>
    public class PayloadMessage : INetworkMessage
    {
        public string SenderId { get; set; }
        public string SenderConnectionId { get; set; }

        /// <summary>
        /// The target's unique identifier if the target
        /// is an entity.
        /// 
        /// Messages that don't have a unique identifier will not be saved by the
        /// server for reckoning
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// The full type name of the payload. This should usually
        /// NOT be set directly, instead call SetPayload to set the
        /// payload and the type at once.
        /// </summary>
        public string? PayloadType { get; set; }

        /// <summary>
        ///  The payload, which is usually some representation of
        ///  an entity's state, serialized into a string. This should
        ///  usually NOT be set directly, instead call SetPayload to
        ///  set the payload and type at once.
        /// </summary>
        public string? Payload { get; set; }

        
        public PayloadMessage(string senderId, string connectionId, string targetId)
        {
            SenderId = senderId;
            SenderConnectionId = connectionId;
            TargetId = targetId;
        }
        
        /// <summary>
        /// Sets the Payload and PayloadType property by serializing 
        /// the provided state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="state"></param>
        public void SetPayload<T>(T state)
        {
            Payload = JsonSerializer.Serialize(state);
            PayloadType = typeof(T).FullName;
        }

        /// <summary>
        /// Attempts to deserialize the payload into the
        /// target type T
        /// </summary>
        /// <typeparam name="T">The payload type</typeparam>
        /// <returns>A desrialized object or an exception</returns>
        /// <exception cref="Exception">An exception thrown when the provided type T 
        /// does not match PayloadType</exception>
        public T GetPayload<T>()
        {
            if(typeof(T).FullName == PayloadType)
            {
                return JsonSerializer.Deserialize<T>(Payload);
            }
            else
            {
                throw new Exception($"Tried to deserialize payload of type {PayloadType} as type {typeof(T).FullName}");
            }
        }

        /// <summary>
        /// Gets a string representation of this message, usually
        /// used for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"{TargetId}({PayloadType})";
        }
    }
}
