namespace TcpFramework
{
    public sealed class TcpServiceOptions
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 9000;
        public int RpcTimeoutMs { get; set; } = ProtocolConstants.DefaultRpcTimeoutMs;
        public int InboundQueueCapacity { get; set; } = 0;
        public QueueOverflowStrategy QueueOverflowStrategy { get; set; } = QueueOverflowStrategy.DropOldest;
        public bool DispatchOnMainThread { get; set; }
        public int MaxFrameSize { get; set; } = ProtocolConstants.DefaultMaxFrameSize;
        public TcpClientOptions Client { get; set; } = new TcpClientOptions();
    }
}
