
namespace SignalRed.Common.Interfaces
{
    public interface INetworkEntity
    {
        public string OwnerClientId { get; set; }

        public string EntityId { get; set; }

        object GetState();

        void ApplyState(object networkState);

        void Destroy();
    }
}