using System;

namespace TcpFramework
{
    /// <summary>
    /// 从网址/地址字符串解析出 host 和 port，用于 ConnectAsync(host, port)。
    /// 支持格式： "host:port"、"tcp://host:port"、"host"（默认端口）。
    /// </summary>
    public static class TcpEndpointParser
    {
        /// <summary>默认端口（未指定时使用）</summary>
        public const int DefaultPort = 9000;

        /// <summary>
        /// 解析地址字符串。
        /// 例如："game.example.com:9000"、"tcp://api.server.com:8080"、"192.168.1.1:9000"。
        /// </summary>
        /// <param name="address">地址字符串，可含 tcp:// 前缀</param>
        /// <param name="defaultPort">未写端口时使用的默认端口</param>
        /// <returns>(host, port)，解析失败时返回 (null, 0)</returns>
        public static (string host, int port) Parse(string address, int defaultPort = DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(address))
                return (null, 0);

            string s = address.Trim();
            if (s.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(6).Trim();

            int lastColon = s.LastIndexOf(':');
            if (lastColon < 0)
                return (s, defaultPort);

            string host = s.Substring(0, lastColon).Trim();
            string portStr = s.Substring(lastColon + 1).Trim();
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(portStr))
                return (s, defaultPort);

            if (!int.TryParse(portStr, out int port) || port <= 0 || port > 65535)
                return (host, defaultPort);

            return (host, port);
        }
    }
}
