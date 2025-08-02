using Netick.Unity;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Netick.Transport
{
    [CreateAssetMenu(fileName = nameof(SimpleWebTransportProvider), menuName = "Netick/Transport/SimpleWebTransportProvider")]
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

            public override int Mtu => 1400;

            public override void Send(IntPtr ptr, int length)
            {
                Peer.Send(ptr, length);
            }

            public override void SendUserData(IntPtr ptr, int length, TransportDeliveryMethod transportDeliveryMethod)
            {
                if (transportDeliveryMethod == TransportDeliveryMethod.Unreliable)
                {
                    Debug.LogWarning($"[{nameof(SimpleWebTransport)}]: SendUserData does not support Unreliable delivery method. Falling back to reliable...");
                    Peer.Send(ptr, length);
                }

                if (transportDeliveryMethod == TransportDeliveryMethod.Reliable)
                {
                    Peer.Send(ptr, length);
                }
            }

            public IEndPoint GetEndPoint()
            {
                return Peer.Endpoint;
            }
        }

        private SimpleWebTransportProvider _transportProvider;
        private Dictionary<SimpleWebsocketPeer, SimpleWebConnection> _connections;
        private Queue<SimpleWebConnection> _freeConnections;
        private NetManager _netManager;
        private BitBuffer _buffer;
        private byte[] _receiveBuffer;

        public SimpleWebTransport(SimpleWebTransportProvider transportProvider)
        {
            _transportProvider = transportProvider;
        }

        public override void Init()
        {
            _netManager = new NetManager();
            _netManager.Init(Engine, this, _transportProvider.SimpleWebConfig);
            _buffer = new BitBuffer(createChunks: false);

            _connections = new(Engine.Config.MaxPlayers);
            _freeConnections = new(Engine.Config.MaxPlayers);

            for (int i = 0; i < Engine.Config.MaxPlayers; i++)
                _freeConnections.Enqueue(new SimpleWebConnection());

            _receiveBuffer = new byte[2048];
        }

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLength)
        {
            if (connectionData == null)
            {
                _netManager.Connect(address, port, null, 0);
                return;
            }

            _netManager.Connect(address, port, connectionData, connectionDataLength);
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
            SimpleWebConnection connection = _freeConnections.Dequeue();
            connection.Peer = peer;

            _connections.Add(peer, connection);
            NetworkPeer.OnConnected(connection);
        }

        void ISimpleWebsocketEventListener.OnPeerDisconnected(SimpleWebsocketPeer peer, DisconnectReason disconnectReason)
        {
            if (Engine.IsClient)
            {
                if (disconnectReason == DisconnectReason.ConnectionFailed || disconnectReason == DisconnectReason.Timeout)
                {
                    NetworkPeer.OnConnectFailed(ConnectionFailedReason.Timeout);
                    return;
                }

                if (disconnectReason == DisconnectReason.ConnectionRejected)
                {
                    NetworkPeer.OnConnectFailed(ConnectionFailedReason.Refused);
                    return;
                }
            }

            if (_connections.ContainsKey(peer))
            {
                SimpleWebConnection connection = _connections[peer];

                _connections.Remove(peer);
                _freeConnections.Enqueue(connection);

                if (disconnectReason == DisconnectReason.Kick)
                {
                    NetworkPeer.OnDisconnected(connection, TransportDisconnectReason.Kick);
                    return;
                }

                if (disconnectReason == DisconnectReason.Timeout)
                {
                    NetworkPeer.OnDisconnected(connection, TransportDisconnectReason.Timeout);
                }
            }
        }

        void ISimpleWebsocketEventListener.OnNetworkReceive(SimpleWebsocketPeer peer, ArraySegment<byte> bytes)
        {
            if (!_connections.TryGetValue(peer, out var c))
                return;

            Array.Copy(bytes.Array, bytes.Offset, _receiveBuffer, 0, bytes.Count);

            fixed (byte* ptr = _receiveBuffer)
            {
                _buffer.SetFrom(ptr, bytes.Count, bytes.Count);
                NetworkPeer.Receive(c, _buffer);
            }
        }

        void ISimpleWebsocketEventListener.OnConnectRequest(SimpleWebsocketPeer peer, SimpleWebConnectionRequest request, byte[] bytes)
        {
            bool accepted = NetworkPeer.OnConnectRequest(bytes, bytes.Length, peer.Endpoint);

            if (accepted)
                request.Accept();
            else
                request.Reject();
        }
    }
}
