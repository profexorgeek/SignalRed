using SignalRed.Common;

SRServer.Instance.Initialize();
await SRServer.Instance.RunAsync(5000);