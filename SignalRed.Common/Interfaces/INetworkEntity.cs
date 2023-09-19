namespace SignalRed.Common.Interfaces
{
    public enum UpdateType
    {
        Unknown = 0,
        Create = 1,
        Update = 2,
        Reckon = 3,
        Delete = 4,
    }

    public interface INetworkEntityState
    {
        string OwnerId { get; }
        string EntityId { get; }
        string EntityType { get; }
        UpdateType UpdateType { get; }
    }
}
