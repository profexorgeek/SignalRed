using Orleans.Runtime;
using Orleans.Streams;
using SignalRed.Common;

namespace SignalRed.Server;

public class ChannelGrain : Grain, IChannelGrain
{
    private readonly List<ChatMessage> messages = new(100);
    private readonly List<string> members = new(10);

    private IAsyncStream<ChatMessage> stream = null!;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider("chat");
        var streamId = StreamId.Create("ChatRoom", this.GetPrimaryKeyString());
        stream = streamProvider.GetStream<ChatMessage>(streamId);
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<StreamId> Join(string nickname)
    {
        members.Add(nickname);
        await stream.OnNextAsync(
            new ChatMessage(
                "SYSTEM",
                $"{nickname} joins the chat '{this.GetPrimaryKeyString()}'..."));
        return stream.StreamId;
    }

    public async Task<StreamId> Leave(string nickname)
    {
        members.Remove(nickname);
        await stream.OnNextAsync(
            new ChatMessage(
                "SYSTEM",
                $"{nickname} leaves the chat..."));
        return stream.StreamId;
    }

    public async Task<bool> Message(ChatMessage message)
    {
        messages.Add(message);
        await stream.OnNextAsync(message);
        return true;
    }

    public Task<string[]> GetMembers() => Task.FromResult(members.ToArray());

    public Task<ChatMessage[]> ReadHistory(int numberOfMessages)
    {
        var response = messages
            .OrderByDescending(m => m.Created)
            .Take(numberOfMessages)
            .OrderBy(m => m.Created)
            .ToArray();

        return Task.FromResult(response);
    }
}
