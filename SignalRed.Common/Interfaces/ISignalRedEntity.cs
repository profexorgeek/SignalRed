
using SignalRed.Common.Messages;

namespace SignalRed.Common.Interfaces
{
    /// <summary>
    /// An ISignalRedEntity is a game entity that can be synchronized across
    /// a network. It requires a serializable and deserializeable state type T
    /// and a 1:1 relationship between entity and state type
    /// </summary>
    /// <typeparam name="T">A type that represents this entities state</typeparam>
    public interface ISignalRedEntity<T>
    {
        /// <summary>
        /// The entity owner's unique client ID. This should generally be set by
        /// the SignalRedClient and not directly altered.
        /// </summary>
        string OwnerClientId { get; set; }

        /// <summary>
        /// The entity's unique ID across the network. This is required to know which
        /// entity to apply an incoming state to. This is usually a combination of
        /// the OwnerClientId and an incrementing integer.
        /// 
        /// This should generally be set by the SignalRedClient and not directly altered.
        /// </summary>
        string EntityId { get; set; }

        /// <summary>
        /// Called when an incoming Creation message is received. Should contain the
        /// initial state to apply to the entity. Contains a deltaSeconds parameter
        /// so interpolation and physics can be applied to compensate for latency.
        /// </summary>
        /// <param name="networkState">The initial state to apply</param>
        /// <param name="deltaSeconds">The estimated elasped time since this state was sent in seconds</param>
        void ApplyCreationState(T networkState, float deltaSeconds);

        /// <summary>
        /// Called when an incoming Update message is received. Should contain the
        /// initial state to apply to the entity. Contains a deltaSeconds parameter
        /// so interpolation and physics can be applied to compensate for latency.
        /// </summary>
        /// <param name="networkState">The initial state to apply</param>
        /// <param name="deltaSeconds">The estimated elasped time since this state was sent in seconds</param>
        void ApplyUpdateState(T networkState, float deltaSeconds, bool force = false);

        /// <summary>
        /// Called when an incoming Destroy message is received.
        /// </summary>
        void Destroy(T networkState, float deltaSeconds);

        /// <summary>
        /// Called when a state needs to be retrieved from the entity and sent through
        /// the network.
        /// </summary>
        T GetState();
    }
}