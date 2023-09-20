using SignalRed.Common.Interfaces;
using System.Text.Json;

namespace SignalRed.Common.Messages
{
    public enum UpdateType
    {
        Unknown = 0,
        Create = 1,
        Update = 2,
        Reckon = 3,
        Delete = 4,
    }

    public class EntityMessage
    {
        public string OwnerId { get; set; }
        public string EntityId { get; set; }
        public string EntityType { get; set; }
        public string Payload { get; private set; }

        public void SetPayload<T>(T state)
        {
            Payload = JsonSerializer.Serialize<T>(state);
        }

        public T GetPayload<T>()
        {
            return JsonSerializer.Deserialize<T>(Payload);
        }
    }
}
