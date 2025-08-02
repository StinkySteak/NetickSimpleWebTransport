using JamesFrowen.SimpleWeb;
using System;

namespace Netick.Transport
{
    public class SimpleWebServerMessageCallback
    {
        public event Action<int, ArraySegment<byte>> OnGameData;

        private SimpleWebServer _webServer;

        public void Init(SimpleWebServer webServer)
        {
            _webServer = webServer;

            _webServer.onData += OnWebServerMessageReceived;
        }

        private void OnWebServerMessageReceived(int connectionId, ArraySegment<byte> message)
        {
            NetManagerPacket packet = (NetManagerPacket)message[0];

            if (packet == NetManagerPacket.Game)
            {
                ArraySegment<byte> bytes = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);

                OnGameData?.Invoke(connectionId, bytes);
            }
        }
    }
}
