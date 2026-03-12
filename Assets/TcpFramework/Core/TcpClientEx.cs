using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TcpFramework
{
    /// <summary>
    /// TCP 客户端：连接、自动重连、收发（长度前缀协议）、心跳。
    /// 注意：OnMessage / OnConnected / OnDisconnected 可能在后台线程触发。
    /// </summary>
    public class TcpClientEx
    {
        public TcpSession Session { get; private set; }
        public HeartbeatManager Heartbeat { get; private set; }

        private string _host;
        private int _port;
        private bool _autoReconnect = true;
        private CancellationTokenSource _cts;

        public event Action<ushort, byte[]> OnMessage;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public async Task ConnectAsync(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _cts = new CancellationTokenSource();
            await TryConnectLoop().ConfigureAwait(false);
        }

        public void Send(ushort msgId, byte[] payload)
        {
            byte[] packet = LengthPrefixedCodec.Encode(msgId, payload ?? Array.Empty<byte>());
            Session?.Send(packet);
        }

        /// <summary>发送原始 body（仅 4 字节长度 + body，无 msgId），一般用于兼容旧逻辑</summary>
        public void SendRaw(byte[] body)
        {
            byte[] packet = LengthPrefixedCodec.EncodeRaw(body ?? Array.Empty<byte>());
            Session?.Send(packet);
        }

        private async Task TryConnectLoop()
        {
            while (_cts != null && !_cts.IsCancellationRequested)
            {
                try
                {
                    var client = new TcpClient();
                    await client.ConnectAsync(_host, _port).ConfigureAwait(false);

                    Session = new TcpSession(client);

                    StartReceive();
                    StartHeartbeat();

                    OnConnected?.Invoke();
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error?.Invoke($"Connect failed: {ex.Message}, retry...");
                    await Task.Delay(3000, _cts.Token).ConfigureAwait(false);
                }
            }
        }

        private void StartReceive()
        {
            Task.Run(async () =>
            {
                var stream = Session.Stream;
                byte[] buf = new byte[4096];

                try
                {
                    while (Session != null && Session.Connected)
                    {
                        int len = await stream.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                        if (len <= 0) break;

                        Session.Buffer.Write(buf, 0, len);

                        foreach (var (msgId, payload) in LengthPrefixedCodec.Decode(Session.Buffer))
                        {
                            Heartbeat?.Refresh();
                            OnMessage?.Invoke(msgId, payload);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch { }

                HandleDisconnect();
            });
        }

        private void StartHeartbeat()
        {
            Heartbeat = new HeartbeatManager(() =>
            {
                Send(ProtocolConstants.HeartbeatMsgId, Array.Empty<byte>());
            });
            Heartbeat.Start();
        }

        private void HandleDisconnect()
        {
            OnDisconnected?.Invoke();
            Session?.Close();
            Session = null;

            if (_autoReconnect && _cts != null && !_cts.IsCancellationRequested)
                _ = TryConnectLoop();
        }

        public void Close()
        {
            _autoReconnect = false;
            _cts?.Cancel();
            Session?.Close();
            Session = null;
        }

        /// <summary>发送文本，msgId 默认为 1</summary>
        public void SendString(string msg, ushort msgId = 1)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(msg ?? "");
            Send(msgId, bytes);
        }
    }
}
