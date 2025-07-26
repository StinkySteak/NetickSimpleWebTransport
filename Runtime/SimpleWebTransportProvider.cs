using Netick.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Netick.Transport
{
    [CreateAssetMenu(fileName = nameof(SimpleWebTransportProvider), menuName = "Netick/SimpleWebTransportProvider")]
    public class SimpleWebTransportProvider : NetworkTransportProvider
    {
        public SimpleWebConfig SimpleWebConfig;

        public override NetworkTransport MakeTransportInstance()
        {
            return new SimpleWebTransport(this);
        }
    }

    public unsafe class SimpleWebTransport : NetworkTransport, ISimpleWebsocketEventListener
    {
        internal class SimpleWebConnection : TransportConnection
        {
            internal SimpleWebsocketPeer Peer;

            public override IEndPoint EndPoint => Peer.Endpoint;

            public override int Mtu => 1200;

            public override void Send(IntPtr ptr, int length)
            {
                Peer.Send(ptr, length);
            }

            public IEndPoint GetEndPoint()
            {
                return Peer.Endpoint;
            }
        }

        private SimpleWebTransportProvider _transportProvider;
        private Dictionary<SimpleWebsocketPeer, SimpleWebConnection> _connectedClients;
        private Queue<SimpleWebConnection> _freeClients;
        private NetManager _netManager;
        private BitBuffer _buffer;

        public SimpleWebTransport(SimpleWebTransportProvider transportProvider)
        {
            _transportProvider = transportProvider;
        }

        public override void Init()
        {
            _netManager = new NetManager();
            _netManager.Init(Engine, this, _transportProvider.SimpleWebConfig);
            _buffer = new BitBuffer(createChunks: false);

            _connectedClients = new(Engine.Config.MaxPlayers);
            _freeClients = new(Engine.Config.MaxPlayers);

            for (int i = 0; i < Engine.Config.MaxPlayers; i++)
                _freeClients.Enqueue(new SimpleWebConnection());
        }

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLength)
        {
            _netManager.Connect(address, port);
        }

        public override void Disconnect(TransportConnection connection)
        {
            _netManager.DisconnectPeer(((SimpleWebConnection)connection).Peer);
        }

        public override void PollEvents()
        {
            _netManager.PollUpdate();
        }

        public override void Run(RunMode mode, int port)
        {
            if (mode == RunMode.Client)
            {
                _netManager.Start();
            }
            if (mode == RunMode.Server)
            {
                _netManager.Start(port);
            }
        }

        public override void Shutdown()
        {
            _netManager.Stop();
        }

        void ISimpleWebsocketEventListener.OnPeerConnected(SimpleWebsocketPeer peer)
        {
            SimpleWebConnection connection = _freeClients.Dequeue();
            connection.Peer = peer;

            _connectedClients.Add(peer, connection);
            NetworkPeer.OnConnected(connection);
        }

        void ISimpleWebsocketEventListener.OnPeerDisconnected(SimpleWebsocketPeer peer)
        {
            SimpleWebConnection connection = _connectedClients[peer];

            _connectedClients.Remove(peer);
            _freeClients.Enqueue(connection);
        }


        void ISimpleWebsocketEventListener.OnNetworkReceive(SimpleWebsocketPeer peer, byte[] bytes)
        {
            if (!_connectedClients.TryGetValue(peer, out var c))
                return;

            fixed (byte* ptr = bytes)
            {
                _buffer.SetFrom(ptr, bytes.Length, bytes.Length);
                NetworkPeer.Receive(c, _buffer);
            }
        }
    }
}
