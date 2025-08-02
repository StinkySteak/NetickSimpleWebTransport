using System;

namespace Netick.Transport
{
    internal class SimpleWebsocketPeer
    {
        private NetManager _netManager;
        private int _connectionId;
        private SimpleWebEndPoint _endpoint;
        private SimpleWebConnectionRequest _connectionRequest;

        public NetManager NetManager => _netManager;
        public int ConnectionId => _connectionId;
        public SimpleWebEndPoint Endpoint => _endpoint;
        public SimpleWebConnectionRequest ConnectionRequest => _connectionRequest;

        public SimpleWebsocketPeer()
        {
            _endpoint = new SimpleWebEndPoint();
        }

        public void SetConnectionRequest(SimpleWebConnectionRequest request)
        {
            _connectionRequest = request;
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
