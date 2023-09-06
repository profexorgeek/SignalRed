using Orleans.Streams;
using SignalRed.Common;
using Spectre.Console;

namespace SignalRed.Client;

public sealed class StreamObserver : IAsyncObserver<ChatMessage>
{
    private readonly string roomName;

    public StreamObserver(string roomName) => this.roomName = roomName;

    public Task OnCompletedAsync() => Task.CompletedTask;

    public Task OnErrorAsync(Exception error)
    {
        AnsiConsole.WriteException(error);
        return Task.CompletedTask;
    }

    public Task OnNextAsync(ChatMessage message, StreamSequenceToken? token = null)
    {
        AnsiConsole.MarkupLine(
            "[[[dim]{0}[/]]][[{1}]] [bold yellow]{2}:[/] {3}",
            message.Created.LocalDateTime,
            roomName,
            message.Author,
            message.Text
            );

        return Task.CompletedTask;
    }
}
