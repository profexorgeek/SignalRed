using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Runtime;
using SignalRed.Client;
using SignalRed.Common;
using Spectre.Console;

// set up localhost client
using var host = new HostBuilder()
    .UseOrleansClient(clientBuilder =>
    {
        clientBuilder.UseLocalhostClustering()
        .AddMemoryStreams("chat");
    })
    .Build();
var client = host.Services.GetRequiredService<IClusterClient>();
var context = new ClientContext(client);

// print the UI framework
PrintUsage();

await StartAsync(host);

context = context with
{
    UserName = AnsiConsole.Ask<string>("What is your [aqua]name[/]?")
};

AnsiConsole.MarkupLine("[bold olive]Welcome [lime]{0}[/] join a channel to chat![/]", context.UserName);

await ProcessLoopAsync(context);
await StopAsync(host);




/// Prints chat system usage
static void PrintUsage()
{
    var h1 = new FigletText("SignalRed")
    {
        Color = Color.Fuchsia,
    };

    var markup = new Markup(
       "[bold fuchsia]/j[/] [aqua]<channel>[/] to [underline green]join[/] a specific channel\n"
       + "[bold fuchsia]/n[/] [aqua]<username>[/] to set your [underline green]name[/]\n"
       + "[bold fuchsia]/l[/] to [underline green]leave[/] the current channel\n"
       + "[bold fuchsia]/h[/] to re-read channel [underline green]history[/]\n"
       + "[bold fuchsia]/m[/] to query [underline green]members[/] in the channel\n"
       + "[bold fuchsia]/exit[/] to exit\n"
       + "[bold aqua]<message>[/] to send a [underline green]message[/]\n");

    var table = new Table()
        .HideHeaders()
        .Border(TableBorder.Square)
        .AddColumn(new TableColumn("content"))
        .AddRow(h1)
        .AddRow(markup);

    AnsiConsole.WriteLine();
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

/// Start connecting to the server
static Task StartAsync(IHost host)
{
    return AnsiConsole.Status().StartAsync("Connecting to server...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Dots);
        ctx.Status = "Connecting...";
        await host.StartAsync();
        ctx.Status = "Connected!";
    });
}

/// Stop server connection
static Task StopAsync(IHost host)
{
    return AnsiConsole.Status().StartAsync("Disconnecting...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Dots);
        await host.StopAsync();
    });
}

static async Task ProcessLoopAsync(ClientContext context)
{
    string? input = null;
    while (input is not "/exit")
    {
        input = Console.ReadLine();

        // empty input command
        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        // exit command
        if (input.StartsWith("/exit") && AnsiConsole.Confirm("Do you really want to exit?"))
        {
            break;
        }

        // change username command
        var firstTwoCharacters = input[..2];
        if (firstTwoCharacters is "/n")
        {
            context = context with { UserName = input.Replace("/n", "").Trim() };
            AnsiConsole.MarkupLine("[dim][[STATUS]][/] Set username to [lime]{0}[/]", context.UserName);
            continue;
        }

        // join/leave channel
        if (firstTwoCharacters switch
        {
            "/j" => JoinChannel(context, input.Replace("/j", "").Trim()),
            "/l" => LeaveChannel(context),
            _ => null
        } is Task<ClientContext> ctxTask)
        {
            context = await ctxTask;
            continue;
        }

        // list info
        if(firstTwoCharacters switch
        {
            "/h" => ShowCurrentChannelHistory(context),
            "/m" => ShowChannelMembers(context),
            _ => null
        } is Task task)
        {
            await task;
            continue;
        }

        // send message
        await SendMessage(context, input);
    }
}

static async Task SendMessage(ClientContext context, string messageText)
{
    var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
    await room.Message(new ChatMessage(context.UserName, messageText));
}

static async Task<ClientContext> JoinChannel(ClientContext context, string channelName)
{
    // if we're in another channel... leave it
    if (context.CurrentChannel is not null &&
        !string.Equals(context.CurrentChannel, channelName, StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine(
            "[bold olive]Leaving channel [/]{0}[bold olive] before joining [/]{1}",
            context.CurrentChannel, channelName);
        await LeaveChannel(context);
    }

    AnsiConsole.MarkupLine("[bold]Joining channel [/]{0}", channelName);
    context = context with { CurrentChannel = channelName };
    await AnsiConsole.Status().StartAsync("Joining channel...", async ctx =>
    {
        var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
        await room.Join(context.UserName!);
        var streamId = StreamId.Create("ChatRoom", context.CurrentChannel!);
        var stream = context.Client.GetStreamProvider("chat").GetStream<ChatMessage>(streamId);
        await stream.SubscribeAsync(new StreamObserver(channelName));
    });

    AnsiConsole.MarkupLine("[bold aqua]Joined channel [/]{0}", context.CurrentChannel);
    return context;
}

static async Task<ClientContext> LeaveChannel(ClientContext context)
{
    AnsiConsole.MarkupLine("[bold olive]Leaving channel [/]{0}", context.CurrentChannel!);

    await AnsiConsole.Status().StartAsync("Leaving channel...", async ctx =>
    {
        var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
        await room.Leave(context.UserName!);
        var streamId = StreamId.Create("ChatRoom", context.CurrentChannel);
        var stream = context.Client.GetStreamProvider("chat").GetStream<ChatMessage>(streamId);
        var subscriptionHandles = await stream.GetAllSubscriptionHandles();
        foreach (var handle in subscriptionHandles)
        {
            await handle.UnsubscribeAsync();
        }
    });

    AnsiConsole.MarkupLine("[bold olive]Left channel[/] {0}", context.CurrentChannel);
    return context with { CurrentChannel = null };

};

static async Task ShowChannelMembers(ClientContext context)
{
    var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
    var members = await room.GetMembers();

    AnsiConsole.Write(new Rule($"Members for '{context.CurrentChannel}'")
    {
        Alignment = Justify.Center,
        Style = Style.Parse("darkgreen")
    });

    foreach(var member in members)
    {
        AnsiConsole.MarkupLine("[bold yellow]{0}[/]", member);
    }

    AnsiConsole.Write(new Rule()
    {
        Alignment = Justify.Center,
        Style = Style.Parse("darkgreen")
    });
}

static async Task ShowCurrentChannelHistory(ClientContext context)
{
    var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
    var history = await room.ReadHistory(1_000);
    AnsiConsole.Write(new Rule($"History for '{context.CurrentChannel}'")
    {
        Alignment = Justify.Center,
        Style = Style.Parse("darkgreen")
    });

    foreach(var msg in history)
    {
        AnsiConsole.MarkupLine("[[[dim]{0}[/]]] [bold yellow]{1}:[/] {2}",
            msg.Created.LocalDateTime, msg.Author, msg.Text);
    }

    AnsiConsole.Write(new Rule($"History for '{context.CurrentChannel}'")
    {
        Alignment = Justify.Center,
        Style = Style.Parse("darkgreen")
    });
}