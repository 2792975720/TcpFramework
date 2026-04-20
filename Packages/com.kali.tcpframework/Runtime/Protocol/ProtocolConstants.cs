namespace TcpFramework
{
    /// <summary>协议常量，便于统一配置</summary>
    public static class ProtocolConstants
    {
        /// <summary>保留消息号：心跳</summary>
        public const ushort HeartbeatMsgId = 0;

        /// <summary>单帧最大长度（含 msgId，不含长度前缀）</summary>
        public const int DefaultMaxFrameSize = 1024 * 1024;

        /// <summary>非法包累计达到该值后主动断开</summary>
        public const int DefaultInvalidPacketDisconnectThreshold = 1;

        public const int DefaultReconnectIntervalMs = 3000;
        public const int DefaultHeartbeatIntervalMs = 3000;
        public const int DefaultRpcTimeoutMs = 5000;
    }
}
