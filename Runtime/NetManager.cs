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

        private Dictionary<int, SimpleWebsocketPeer> _activePeers = new();
        private List<SimpleWebsocketPeer> _waitingPeers;
        private ISimpleWebsocketEventListener _listener;
        private SimpleWebConfig _simpleWebConfig;

        private Queue<SimpleWebsocketPeer> _freePeers;
        private const string KeyConnectionData = "connectionData";

        internal void Init(NetickEngine engine, ISimpleWebsocketEventListener listener, SimpleWebConfig simpleWebConfig)
        {
            _engine = engine;
            _listener = listener;
            _simpleWebConfig = simpleWebConfig;

            _freePeers = new(engine.Config.MaxPlayers);
            _waitingPeers = new(engine.Config.MaxPlayers);
            _activePeers = new(engine.Config.MaxPlayers);

            for (int i = 0; i < engine.Config.MaxPlayers; i++)
                _freePeers.Enqueue(new SimpleWebsocketPeer());
        }

        public void Start() { }

        public void Start(int port)
        {
            TcpConfig tcpConfig = new TcpConfig(noDelay: false, sendTimeout: 5000, receiveTimeout: 20000);
            _webServer = new SimpleWebServer(5000, tcpConfig, ushort.MaxValue, 5000, new SslConfig());

            _webServer.Start((ushort)port);
            _webServer.onConnect += OnRemoteClientConnected;
            _webServer.onData += OnWebServerMessageReceived;
            _webServer.onDisconnect += OnRemoteClientDisconnected;
        }

        public void Stop()
        {
            _webServer?.Stop();
            _webClient?.Disconnect();
        }

        private void OnRemoteClientDisconnected(int connectionId)
        {
            bool wasActivePeer = _activePeers.TryGetValue(connectionId, out SimpleWebsocketPeer activePeer);

            if (wasActivePeer)
            {
                _activePeers.Remove(connectionId);

                _listener.OnPeerDisconnected(activePeer, DisconnectReason.Timeout);

                activePeer.Reset();
                _freePeers.Enqueue(activePeer);
                return;
            }
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
            if (!_activePeers.TryGetValue(connectionId, out SimpleWebsocketPeer peer))
                return;

            _listener.OnNetworkReceive(peer, bytes.ToArray());
        }

        private void OnRemoteClientConnected(int connectionId)
        {
            _webServer.GetClientEndPoint(connectionId, out string address, out int port);

            SimpleWebsocketPeer peer = _freePeers.Dequeue();
            peer.Init(this, address, port, connectionId);

            SimpleWebConnectionRequest request = new SimpleWebConnectionRequest();

            if (_webServer.GetClientRequest(connectionId).RequestLine != null)
            {
                string requestLine = _webServer.GetClientRequest(connectionId).RequestLine;

                string[] parts = requestLine.Split(' ');
                string pathAndQuery = parts[1];

                Uri uri = new Uri("wss://dummy" + pathAndQuery);
                string base64Encoded = System.Web.HttpUtility.ParseQueryString(uri.Query)[KeyConnectionData];

                byte[] bytes = SimpleWebConnectionPayload.BytesFromWebSocketBase64(base64Encoded);

                request.SetData(bytes);
                peer.SetConnectionRequest(request);

                _waitingPeers.Add(peer);
                _listener.OnConnectRequest(peer, request, bytes);
                return;
            }

            _waitingPeers.Add(peer);
            _listener.OnConnectRequest(peer, request, Array.Empty<byte>());
        }

        private void OnConnectionRequestComplete(SimpleWebsocketPeer peer, SimpleWebConnectionRequest request)
        {
            if (request.IsAccepted)
            {
                _activePeers.Add(peer.ConnectionId, peer);
                _listener.OnPeerConnected(peer);
                return;
            }

            _freePeers.Enqueue(peer);
            _webServer.KickClient(peer.ConnectionId);
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

            NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);
            string connectionPayload = string.Empty;

            if (connectionData != null)
            {
                connectionPayload = SimpleWebConnectionPayload.BytesToWebSocketBase64(connectionData);
            }

            query.Set(KeyConnectionData, connectionPayload);
            builder.Query = query.ToString();

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

                for (int i = _waitingPeers.Count - 1; i >= 0; i--)
                {
                    SimpleWebsocketPeer peer = _waitingPeers[i];

                    if (peer.ConnectionRequest.IsComplete)
                    {
                        _waitingPeers.RemoveAt(i);
                        OnConnectionRequestComplete(peer, peer.ConnectionRequest);
                    }
                }
            }

            if (_engine.IsClient)
            {
                _webClient?.ProcessMessageQueue();
            }
        }
    }
}
