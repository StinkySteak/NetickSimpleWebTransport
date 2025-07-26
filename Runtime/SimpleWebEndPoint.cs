namespace Netick.Transport
{
    public class SimpleWebEndPoint : IEndPoint
    {
        public SimpleWebEndPoint(string address, int port)
        {
            _ipAddress = address;
            _port = port;
        }

        private string _ipAddress;
        private int _port;

        public string IPAddress => _ipAddress;

        public int Port => _port;

        public override string ToString()
        {
            return string.Format("{0}:{1}", _ipAddress, _port);
        }
    }
}
