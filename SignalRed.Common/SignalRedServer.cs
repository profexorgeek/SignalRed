using SignalRed.Common.Hubs;
using SignalRed.Common.Interfaces;

namespace SignalRed.Common
{
    public class SignalRedServer
    {
        private static SignalRedServer instance;
        private bool initialized = false;
        WebApplication? server;

        public static SignalRedServer Instance => instance ?? (instance = new SignalRedServer());

        /// <summary>
        /// Initialization method, must be called before attempting to use the service
        /// </summary>
        public void Initialize()
        {
            initialized = true;
        }

        /// <summary>
        /// Starts the server hosting on the specified port.
        /// </summary>
        /// <param name="port">The port to host on</param>
        /// <returns>A task representing the server's executing process</returns>
        /// <exception cref="Exception">Exception thrown if server is run before initialization</exception>
        public async Task RunAsync(int port)
        {
            if(!initialized)
            {
                throw new Exception("Attempted to start server without initializing SRServer service!");
            }

            // disconnect any existing server
            await StopAsync();

            // create the web application builder
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSignalR();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(port);
            });

            // build and start the server
            server = builder.Build();
            server.UseRouting();
            server.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<GameHub>("/game");
            });
            await server.RunAsync();
        }

        /// <summary>
        /// Stops the server
        /// </summary>
        /// <returns>A task representing the stop progress</returns>
        public async Task StopAsync()
        {
            if(server != null)
            {
                await server.StopAsync();
            }
        }

    }
}
