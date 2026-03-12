using System;
using System.Collections.Generic;

namespace TcpFramework
{
    /// <summary>按消息类型注册回调，将 (msgId, payload) 派发到对应 IMessage 处理器。</summary>
    public class MessageDispatcher
    {
        private readonly Dictionary<ushort, Action<byte[]>> _handlers = new Dictionary<ushort, Action<byte[]>>();

        public void Register<T>(Action<T> handler) where T : IMessage, new()
        {
            var temp = new T();
            _handlers[temp.MsgId] = data =>
            {
                var msg = new T();
                msg.Deserialize(data ?? Array.Empty<byte>());
                handler(msg);
            };
        }

        public void Dispatch(ushort msgId, byte[] payload)
        {
            if (_handlers.TryGetValue(msgId, out var cb))
                cb(payload);
        }
    }
}
