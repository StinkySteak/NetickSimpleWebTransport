using UnityEngine;

namespace Netick.Transport
{
    public class SimpleWebConnectionRequest
    {
        private bool _isComplete;
        private bool _isAccepted;
        private byte[] _data;

        public bool IsComplete => _isComplete;
        public bool IsAccepted => _isAccepted;
        public byte[] Data => _data;

        public void Accept()
        {
            _isComplete = true;
            _isAccepted = true;
        }

        public void Reject()
        {
            _isComplete = true;
            _isAccepted = false;
        }

        internal void SetData(byte[] data)
            => _data = data;
    }
}
