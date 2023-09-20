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
        public string Id { get; set; }
        public string Owner { get; set; }
        public string PayloadType { get; set; }
        public string Payload { get; set; }

        public void SetPayload<T>(T state)
        {
            Payload = JsonSerializer.Serialize(state);
            PayloadType = typeof(T).FullName;
        }

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

        public override string ToString()
        {
            return $"{PayloadType} ID:{Id}";
        }
    }
}
