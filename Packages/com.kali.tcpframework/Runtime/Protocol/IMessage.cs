namespace TcpFramework
{
    /// <summary>可序列化消息接口，用于 <see cref="MessageDispatcher"/> 与 <see cref="TcpService"/>。</summary>
    public interface IMessage
    {
        ushort MsgId { get; }
        byte[] Serialize();
        void Deserialize(byte[] data);
    }
}
