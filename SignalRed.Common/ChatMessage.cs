namespace SignalRed.Common;

[GenerateSerializer]
public record class ChatMessage(string? Author, string Text)
{
    [Id(0)]
    public string Author { get; init; } = Author ?? "Unknown";

    [Id(1)]
    public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;

}
