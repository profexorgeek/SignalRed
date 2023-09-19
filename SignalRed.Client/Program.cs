using Microsoft.AspNetCore.SignalR.Client;
using SignalRed.Common.Messages;

namespace SignalRed.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var uri = new Uri("http://localhost:5006");
        SRClient.Instance.Initialize();
        SRClient.Instance.Connect(uri);

        Console.WriteLine("Enter chat message:\n");
        while(true)
        {
            if(Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if(key == ConsoleKey.Escape)
                {
                    break;
                }
            }

            string msg = Console.ReadLine() ?? "";
            await SRClient.Instance.SendMessage(msg);
        }

        await SRClient.Instance.Disconnect();
    }
}

