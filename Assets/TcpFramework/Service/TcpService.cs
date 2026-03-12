using System;
using System.Threading.Tasks;

namespace TcpFramework
{
    /// <summary>
    /// 单例形式的 TCP 客户端门面：连接 + 基于 IMessage 的消息派发。
    /// 适合简单客户端场景，业务通过 Register&lt;T&gt; 注册消息处理。
    /// </summary>
    public class TcpService
    {
        public static TcpService Instance { get; } = new TcpService();

        private TcpClientEx _client;
        private readonly MessageDispatcher _dispatcher = new MessageDispatcher();

        public bool Connected => _client?.Session != null && _client.Session.Connected;

        public async Task StartClientAsync(string host, int port)
        {
            _client = new TcpClientEx();
            _client.OnMessage += OnRawMessage;
            _client.OnConnected += () => Log.Info?.Invoke("TcpService Connected");
            _client.OnDisconnected += () => Log.Info?.Invoke("TcpService Disconnected");
            await _client.ConnectAsync(host, port).ConfigureAwait(false);
        }

        public void Send(IMessage msg)
        {
            if (msg == null) return;
            byte[] payload = msg.Serialize() ?? Array.Empty<byte>();
            _client?.Send(msg.MsgId, payload);
        }

        private void OnRawMessage(ushort msgId, byte[] payload)
        {
            _dispatcher.Dispatch(msgId, payload);
        }

        public void Register<T>(Action<T> handler) where T : IMessage, new()
        {
            _dispatcher.Register(handler);
        }
    }
}
