using System;

namespace TcpFramework
{
    public class BinaryMessageSerializer : IMessageSerializer
    {
        public byte[] Serialize(IMessage message)
        {
            return message?.Serialize() ?? Array.Empty<byte>();
        }

        public T Deserialize<T>(byte[] payload) where T : IMessage, new()
        {
            var msg = new T();
            msg.Deserialize(payload ?? Array.Empty<byte>());
            return msg;
        }

        public IMessage Deserialize(Type messageType, byte[] payload)
        {
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));
            if (!typeof(IMessage).IsAssignableFrom(messageType))
                throw new ArgumentException("messageType must implement IMessage.", nameof(messageType));

            var msg = (IMessage)Activator.CreateInstance(messageType);
            msg.Deserialize(payload ?? Array.Empty<byte>());
            return msg;
        }
    }
}
