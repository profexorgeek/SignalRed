using SignalRed.Common.Hubs;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSignalR();

        var app = builder.Build();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<GameHub>("/game");
        });
        app.Run();
    }
}


