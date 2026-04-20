using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace TcpFramework
{
    public sealed class JsonMessageSerializer : IMessageSerializer
    {
        public JsonMessageSerializer() { }

        public byte[] Serialize(IMessage message)
        {
            if (message == null) return Array.Empty<byte>();
            try
            {
                var serializer = new DataContractJsonSerializer(message.GetType());
                using (var ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, message);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogLevel.Error, "Json serialization failed.",
                    Log.Fields(("serializer", nameof(JsonMessageSerializer)), ("messageType", message.GetType().Name)), ex);
                throw;
            }
        }

        public T Deserialize<T>(byte[] payload) where T : IMessage, new()
        {
            if (payload == null || payload.Length == 0) return new T();
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var ms = new MemoryStream(payload))
                {
                    var obj = serializer.ReadObject(ms);
                    if (obj is T msg) return msg;
                    return new T();
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogLevel.Error, "Json deserialize<T> failed.",
                    Log.Fields(("serializer", nameof(JsonMessageSerializer)), ("messageType", typeof(T).Name), ("payloadBytes", payload.Length)), ex);
                throw;
            }
        }

        public IMessage Deserialize(Type messageType, byte[] payload)
        {
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));
            if (!typeof(IMessage).IsAssignableFrom(messageType))
                throw new ArgumentException("messageType must implement IMessage.", nameof(messageType));

            if (payload == null || payload.Length == 0)
                return (IMessage)Activator.CreateInstance(messageType);

            try
            {
                var serializer = new DataContractJsonSerializer(messageType);
                using (var ms = new MemoryStream(payload))
                {
                    var msg = serializer.ReadObject(ms) as IMessage;
                    return msg ?? (IMessage)Activator.CreateInstance(messageType);
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogLevel.Error, "Json deserialize(Type) failed.",
                    Log.Fields(("serializer", nameof(JsonMessageSerializer)), ("messageType", messageType.Name), ("payloadBytes", payload.Length)), ex);
                throw;
            }
        }
    }
}
