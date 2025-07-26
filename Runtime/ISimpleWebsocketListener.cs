namespace Netick.Transport
{
    internal interface ISimpleWebsocketEventListener
    {
        void OnPeerConnected(SimpleWebsocketPeer peer);
        void OnPeerDisconnected(SimpleWebsocketPeer peer);
        void OnNetworkReceive(SimpleWebsocketPeer peer, byte[] bytes);
    }
}
