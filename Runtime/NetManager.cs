using JamesFrowen.SimpleWeb;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Netick.Transport
{
    internal unsafe class NetManager
    {
        private SimpleWebServer _webServer;
        private SimpleWebClient _webClient;
        private SimpleWebsocketPeer _serverConnection;

        private NetickEngine _engine;

        private Dictionary<int, SimpleWebsocketPeer> _peers = new();
        private ISimpleWebsocketEventListener _listener;
        private SimpleWebConfig _simpleWebConfig;

        internal void Init(NetickEngine engine, ISimpleWebsocketEventListener listener, SimpleWebConfig simpleWebConfig)
        {
            _engine = engine;
            _listener = listener;
            _simpleWebConfig = simpleWebConfig;
        }

        public void Start() { }

        public void Start(int port)
        {
            TcpConfig tcpConfig = new TcpConfig(noDelay: false, sendTimeout: 5000, receiveTimeout: 20000);
            _webServer = new SimpleWebServer(5000, tcpConfig, ushort.MaxValue, 5000, new SslConfig());

            _webServer.Start((ushort)port);
            _webServer.onConnect += OnConnect;
            _webServer.onData += OnWebServerMessageReceived;
            _webServer.onDisconnect += OnDisconnected;
        }

        public void Stop()
        {
            _webServer?.Stop();
            _webClient?.Disconnect();
        }

        private void OnDisconnected(int connectionId)
        {
            SimpleWebsocketPeer peer = _peers[connectionId];

            _peers.Remove(connectionId);

            _listener.OnPeerDisconnected(peer);
        }

        internal void DisconnectPeer(SimpleWebsocketPeer peer)
        {
            if (peer.ConnectionId == 0)
            {
                _webClient.Disconnect();
                return;
            }

            _webServer.KickClient(peer.ConnectionId);
        }

        private void OnWebServerMessageReceived(int connectionId, ArraySegment<byte> bytes)
        {
            SimpleWebsocketPeer peer = _peers[connectionId];

            _listener.OnNetworkReceive(peer, bytes.ToArray());
        }

        private void OnConnect(int connectionId)
        {
            string endpoint = _webServer.GetClientAddress(connectionId);
            SimpleWebsocketPeer peer = new SimpleWebsocketPeer(this, endpoint, connectionId);

            _peers.Add(connectionId, peer);

            _listener.OnPeerConnected(peer);
        }

        internal void Send(SimpleWebsocketPeer peer, IntPtr ptr, int length)
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);

            if (peer.ConnectionId > 0)
            {
                _webServer.SendOne(peer.ConnectionId, new ArraySegment<byte>(bytes));
            }
            else
            {
                _webClient.Send(new ArraySegment<byte>(bytes));
            }
        }

        public void Connect(string address, int port)
        {
            TcpConfig tcpConfig = new TcpConfig(noDelay: false, sendTimeout: 5000, receiveTimeout: 20000);
            _webClient = SimpleWebClient.Create(ushort.MaxValue, 5000, tcpConfig);

            UriBuilder builder = new UriBuilder
            {
                Scheme = _simpleWebConfig.ConnectSecurely ? "wss" : "ws",
                Host = address,
                Port = port
            };

            _webClient.Connect(builder.Uri);
            _webClient.onConnect += OnWebClientConnected;
            _webClient.onDisconnect += OnWebClientDisconnected;
            _webClient.onData += OnWebClientMessageReceived;
        }

        private void OnWebClientMessageReceived(ArraySegment<byte> bytes)
        {
            _listener.OnNetworkReceive(_serverConnection, bytes.ToArray());
        }

        private void OnWebClientDisconnected()
        {
            UnityEngine.Debug.Log("OnWebClientDisconnected");
            _listener.OnPeerDisconnected(_serverConnection);
        }


        private void OnWebClientConnected()
        {
            _serverConnection = new SimpleWebsocketPeer(this, string.Empty, 0);

            _listener.OnPeerConnected(_serverConnection);
        }

        public void PollUpdate()
        {
            if (_engine.IsServer)
            {
                _webServer.ProcessMessageQueue();
            }

            if (_engine.IsClient)
            {
                _webClient?.ProcessMessageQueue();
            }
        }
    }
}
