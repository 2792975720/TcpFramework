namespace TcpFramework
{
    public sealed class TcpClientOptions
    {
        public int ReconnectIntervalMs { get; set; } = ProtocolConstants.DefaultReconnectIntervalMs;
        public int HeartbeatIntervalMs { get; set; } = ProtocolConstants.DefaultHeartbeatIntervalMs;
        public int InvalidPacketDisconnectThreshold { get; set; } = ProtocolConstants.DefaultInvalidPacketDisconnectThreshold;
    }
}
