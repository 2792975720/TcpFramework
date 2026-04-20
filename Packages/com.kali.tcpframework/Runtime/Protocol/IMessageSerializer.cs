using System;

namespace TcpFramework
{
    public interface IMessageSerializer
    {
        byte[] Serialize(IMessage message);
        T Deserialize<T>(byte[] payload) where T : IMessage, new();
        IMessage Deserialize(Type messageType, byte[] payload);
    }
}
