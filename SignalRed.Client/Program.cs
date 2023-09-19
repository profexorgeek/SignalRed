using SignalRed.Common.Interfaces;
using SignalRed.Common.Messages;

namespace SignalRed.Client;

class Program
{
    static IGameClient client = new ConsoleGameClient();

    static async Task Main(string[] args)
    {
        var uri = new Uri("http://localhost:5000");
        var rand = new Random();

        var gameClient = new ConsoleGameClient();
        await gameClient.Test();

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape)
                {
                    break;
                }
            }
        }
        await SRClient.Instance.DisconnectAsync();
    }
}

