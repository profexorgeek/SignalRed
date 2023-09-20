
using SignalRed.Common.Messages;

namespace SignalRed.Common.Interfaces
{
    public interface INetworkedGameEntity
    {
        public string Id { get; set; }
        public string Owner { get; set; }
        public string GetSerializedState();
        public void ApplySerializedState(string state);
    }
}
