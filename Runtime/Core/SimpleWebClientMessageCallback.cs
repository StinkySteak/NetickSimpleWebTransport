using JamesFrowen.SimpleWeb;
using System;

namespace Netick.Transport
{
    public class SimpleWebClientMessageCallback
    {
        public event Action<ArraySegment<byte>> OnGameData;
        public event Action OnConnectionRequestApproved;

        private SimpleWebClient _webClient;

        public void Init(SimpleWebClient webClient)
        {
            _webClient = webClient;

            _webClient.onData += OnWebClientMessageReceived;
        }

        private void OnWebClientMessageReceived(ArraySegment<byte> message)
        {
            NetManagerPacket dataType = (NetManagerPacket)message[0];

            if (dataType == NetManagerPacket.Game)
            {
                ArraySegment<byte> bytes = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);
                OnGameData?.Invoke(bytes);
            }
            if (dataType == NetManagerPacket.ConnectionApproved)
            {
                OnConnectionRequestApproved?.Invoke();
            }
        }
    }
}
