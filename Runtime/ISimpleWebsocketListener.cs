namespace Netick.Transport
{
    internal interface ISimpleWebsocketEventListener
    {
        void OnPeerConnected(SimpleWebsocketPeer peer);
        void OnPeerDisconnected(SimpleWebsocketPeer peer, DisconnectReason disconnectReason);
        void OnNetworkReceive(SimpleWebsocketPeer peer, byte[] bytes);
    }

    public enum DisconnectReason
    {
        ConnectionFailed,
        ConnectionRejected,
        Timeout,
        Kick
    }
}
