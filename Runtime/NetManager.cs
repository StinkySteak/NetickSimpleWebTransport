using JamesFrowen.SimpleWeb;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace Netick.Transport
{
    internal unsafe class NetManager
    {
        private SimpleWebServer _webServer;
        private SimpleWebClient _webClient;
        private SimpleWebsocketPeer _serverConnection;
        private SimpleWebEndPoint _serverConnectionCandidateEndpoint;

        private NetickEngine _engine;

        private Dictionary<int, SimpleWebsocketPeer> _peers = new();
        private ISimpleWebsocketEventListener _listener;
        private SimpleWebConfig _simpleWebConfig;

        private Queue<SimpleWebsocketPeer> _freePeers;

        internal void Init(NetickEngine engine, ISimpleWebsocketEventListener listener, SimpleWebConfig simpleWebConfig)
        {
            _engine = engine;
            _listener = listener;
            _simpleWebConfig = simpleWebConfig;

            _freePeers = new Queue<SimpleWebsocketPeer>(engine.Config.MaxPlayers);

            for (int i = 0; i < engine.Config.MaxPlayers; i++)
                _freePeers.Enqueue(new SimpleWebsocketPeer());
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

            _listener.OnPeerDisconnected(peer, DisconnectReason.Timeout);

            _freePeers.Enqueue(peer);
            peer.Reset();
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
            _webServer.GetClientEndPoint(connectionId, out string address, out int port);

            SimpleWebsocketPeer peer = _freePeers.Dequeue();
            peer.Init(this, address, port, connectionId);

            _peers.Add(connectionId, peer);

            foreach (var h in _webServer.GetClientRequest(connectionId).Headers)
            {
                UnityEngine.Debug.Log($"[{h.Key}] = {h.Value}");
            }

            UnityEngine.Debug.Log($"{_webServer.GetClientRequest(connectionId).RequestLine}");

            string requestLine = "/?connectionData=SW0gbGVhcm5pbmcgQyMsIHBsZWFzZSBjb25uZWN0IG1lIHRvIHRoZSBzZXJ2ZXI HTTP/1.1";

            string[] parts = requestLine.Split(' ');
            string pathAndQuery = parts[0];

            Uri uri = new Uri("wss://dummy" + pathAndQuery);
            string base64Encoded = System.Web.HttpUtility.ParseQueryString(uri.Query)["connectionData"];

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

        public void Connect(string address, int port, byte[] connectionData, int connectionDataLength)
        {
            TcpConfig tcpConfig = new TcpConfig(noDelay: false, sendTimeout: 5000, receiveTimeout: 20000);
            _webClient = SimpleWebClient.Create(ushort.MaxValue, 5000, tcpConfig);

            UriBuilder builder = new UriBuilder
            {
                Scheme = _simpleWebConfig.ConnectSecurely ? "wss" : "ws",
                Host = address,
                Port = port,
            };

            if (connectionData != null)
            {
                string base64 = SimpleWebConnectionPayload.BytesToWebSocketBase64(connectionData);
                NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);
                query.Set("connectionData", base64);
                builder.Query = query.ToString();
            }

            _webClient.Connect(builder.Uri);
            _webClient.onConnect += OnWebClientConnected;
            _webClient.onDisconnect += OnWebClientDisconnected;
            _webClient.onData += OnWebClientMessageReceived;

            _serverConnectionCandidateEndpoint = new SimpleWebEndPoint();
            _serverConnectionCandidateEndpoint.Init(address, port);
        }

        private void OnWebClientMessageReceived(ArraySegment<byte> bytes)
        {
            _listener.OnNetworkReceive(_serverConnection, bytes.ToArray());
        }

        private void OnWebClientDisconnected()
        {
            bool hasConnected = _serverConnection != null;

            _listener.OnPeerDisconnected(_serverConnection, hasConnected ? DisconnectReason.Timeout : DisconnectReason.ConnectionFailed);
        }


        private void OnWebClientConnected()
        {
            _serverConnection = _freePeers.Dequeue();
            _serverConnection.Init(this, _serverConnectionCandidateEndpoint.IPAddress, _serverConnectionCandidateEndpoint.Port, 0);

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
