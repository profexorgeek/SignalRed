
namespace SignalRed.Common.Interfaces
{
    public interface INetworkEntity
    {
        public string OwnerClientId { get; set; }

        public string EntityId { get; set; }

        T GetState<T>();

        void ApplyState<T>(T networkState);

        void Destroy();
    }
}