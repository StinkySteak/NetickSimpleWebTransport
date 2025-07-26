using System;

namespace Netick.Transport
{
    internal class SimpleWebsocketPeer
    {
        private NetManager _netManager;
        private SimpleWebEndPoint _endpoint;
        private int _connectionId;

        public int ConnectionId => _connectionId;
        public NetManager NetManager => _netManager;
        public SimpleWebEndPoint Endpoint => _endpoint;

        public SimpleWebsocketPeer(NetManager netManager, string address, int connectionId)
        {
            _netManager = netManager;
            _endpoint = new SimpleWebEndPoint(address, 0);
            _connectionId = connectionId;
        }

        public void Send(IntPtr ptr, int length)
        {
            _netManager.Send(this, ptr, length);
        }
    }
}
