using SignalRed.Client;
using SignalRed.Common.Interfaces;

namespace SignalRed.Common.Messages
{
    public class NetworkMessage : INetworkMessage
    {
        public string SenderClientId { get; set; }
        public string SenderConnectionId { get; set; }
        public double SendTime { get; set; }
        public float DeltaSeconds
        {
            get
            {
                var delta = SignalRedClient.Instance.ServerTime - SendTime;
                delta = Math.Max(delta, 0);
                return (float)(delta / 1000f);
            }
        }

        /// <summary>
        /// This empty constructor shouldn't be called but must exist so
        /// System.Text.Json can properly serialize/deserialize this object!
        /// </summary>
        public NetworkMessage() { }

        public NetworkMessage(string senderClientId, string senderConnectionId, double sendTime)
        {
            SenderClientId = senderClientId;
            SenderConnectionId = senderConnectionId;
            SendTime = sendTime;
        }
    }
}
