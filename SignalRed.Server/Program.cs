using SignalRed.Common;

SignalRedServer.Instance.Initialize();
await SignalRedServer.Instance.RunAsync(5000);