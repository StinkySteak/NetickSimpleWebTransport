namespace Netick.Transport
{
    public class SimpleWebEndPoint : IEndPoint
    {
        public void Init(string address, int port)
        {
            _ipAddress = address;
            _port = port;
        }

        internal void Reset()
        {
            _ipAddress = string.Empty;
            _port = 0;
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
