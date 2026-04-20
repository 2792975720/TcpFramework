using System;
using System.Collections.Generic;

namespace TcpFramework
{
    /// <summary>按消息类型注册回调，将 (msgId, payload) 派发到对应 IMessage 处理器。</summary>
    public class MessageDispatcher
    {
        private readonly Dictionary<ushort, Action<int, byte[]>> _handlers = new Dictionary<ushort, Action<int, byte[]>>();
        private readonly IMessageSerializer _serializer;
        public Action<ushort, Exception> OnDispatchError { get; set; }

        public MessageDispatcher(IMessageSerializer serializer = null)
        {
            _serializer = serializer ?? new BinaryMessageSerializer();
        }

        public void Register<T>(Action<T> handler) where T : IMessage, new()
        {
            var temp = new T();
            _handlers[temp.MsgId] = (_, data) =>
            {
                var msg = _serializer.Deserialize<T>(data);
                handler(msg);
            };
        }

        public void Dispatch(ushort msgId, int requestId, byte[] payload)
        {
            if (_handlers.TryGetValue(msgId, out var cb))
            {
                try
                {
                    cb(requestId, payload);
                }
                catch (Exception ex)
                {
                    OnDispatchError?.Invoke(msgId, ex);
                    Log.Write(LogLevel.Error, "Dispatch failed.",
                        Log.Fields(("msgId", msgId), ("requestId", requestId)), ex);
                }
            }
        }
    }
}
