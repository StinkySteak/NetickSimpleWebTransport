using System;

namespace Netick.Transport
{
    internal static class SimpleWebConnectionPayload
    {
        internal static string BytesToWebSocketBase64(byte[] bytes)
        {
            string base64 = Convert.ToBase64String(bytes);
            string base64UrlSafe = base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
            return base64UrlSafe;
        }

        internal static byte[] BytesFromWebSocketBase64(string query)
        {
            string base64Data = query
            .Replace('-', '+')
            .Replace('_', '/');

            int padding = 4 - (base64Data.Length % 4);
            if (padding < 4)
                base64Data = base64Data.PadRight(base64Data.Length + padding, '=');

            byte[] bytes = Convert.FromBase64String(base64Data);
            return bytes;
        }
    }
}
