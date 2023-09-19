namespace SignalRed.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var uri = new Uri("http://localhost:5006");
        
        // get a username
        Console.WriteLine("Enter your username...");
        string user = Console.ReadLine().Trim();

        // connect to the server as this user
        SRClient.Instance.Initialize();
        SRClient.Instance.Connect(uri, user);

        // handle chat
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

