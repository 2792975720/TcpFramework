using System.Collections.Generic;

namespace TcpFramework
{
    public enum TcpMessageDirection
    {
        Inbound,
        Outbound
    }

    public sealed class TcpMessageContext
    {
        public ushort MsgId { get; set; }
        public int RequestId { get; set; }
        public byte[] Payload { get; set; }
        public TcpMessageDirection Direction { get; set; }
        public Dictionary<string, object> Items { get; } = new Dictionary<string, object>();
    }
}
