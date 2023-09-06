using Orleans.Runtime;

namespace SignalRed.Common;

public interface IChannelGrain : IGrainWithStringKey
{
    Task<StreamId> Join(string nickname);
    Task<StreamId> Leave(string nickname);
    Task<bool> Message(ChatMessage message);
    Task<ChatMessage[]> ReadHistory(int numberOfMessages);
    Task<string[]> GetMembers();
}