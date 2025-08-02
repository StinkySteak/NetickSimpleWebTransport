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
                byte[] bytes = message.ToArray();
                int packetGameLength = bytes.Length - 1;
                byte[] packetGame = new byte[packetGameLength];

                Array.Copy(bytes, 1, packetGame, 0, packetGameLength);
                OnGameData?.Invoke(packetGame);
            }
            if (dataType == NetManagerPacket.ConnectionApproved)
                OnConnectionRequestApproved?.Invoke();
        }
    }
}
