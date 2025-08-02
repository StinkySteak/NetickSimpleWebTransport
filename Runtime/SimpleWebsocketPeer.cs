using System;

namespace Netick.Transport
{
    internal class SimpleWebsocketPeer
    {
        private NetManager _netManager;
        private int _connectionId;
        private SimpleWebEndPoint _endpoint;

        public NetManager NetManager => _netManager;
        public int ConnectionId => _connectionId;
        public SimpleWebEndPoint Endpoint => _endpoint;

        public SimpleWebsocketPeer()
        {
            _endpoint = new SimpleWebEndPoint();
        }

        public void Init(NetManager netManager, string address, int port, int connectionId)
        {
            _netManager = netManager;
            _connectionId = connectionId;
            _endpoint.Init(address, port);
        }

        public void Reset()
        {
            _netManager = null;
            _connectionId = 0;
            _endpoint.Reset();
        }

        public void Send(IntPtr ptr, int length)
        {
            _netManager.Send(this, ptr, length);
        }
    }
}
