using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TcpFramework
{
    /// <summary>
    /// 单条 TCP 连接会话：收发流、长度前缀协议解码、断开事件。
    /// 注意：OnMessage / OnDisconnected 可能在后台线程触发，Unity 中需派发到主线程。
    /// </summary>
    public class TcpSession
    {
        public TcpClient Client { get; private set; }
        public NetworkStream Stream { get; private set; }
        public PacketBuffer Buffer { get; } = new PacketBuffer();

        private readonly object _sendLock = new object();
        private bool _closed;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public event Action<ushort, byte[]> OnMessage;
        public event Action OnDisconnected;

        public bool Connected => Client != null && Client.Connected;

        public TcpSession(TcpClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Stream = client.GetStream();
            _ = ReceiveLoop(_cts.Token);
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            byte[] recv = new byte[4096];

            try
            {
                while (!token.IsCancellationRequested && Connected)
                {
                    int len = await Stream.ReadAsync(recv, 0, recv.Length, token).ConfigureAwait(false);
                    if (len == 0) break;

                    Buffer.Write(recv, 0, len);

                    foreach (var (msgId, payload) in LengthPrefixedCodec.Decode(Buffer))
                        OnMessage?.Invoke(msgId, payload);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* 断开或异常，忽略 */ }
            finally
            {
                Close();
                OnDisconnected?.Invoke();
            }
        }

        public bool Send(ushort msgId, byte[] payload)
        {
            byte[] packet = LengthPrefixedCodec.Encode(msgId, payload);
            return Send(packet);
        }

        public bool Send(byte[] data)
        {
            if (_closed || data == null) return false;

            lock (_sendLock)
            {
                try
                {
                    if (Stream == null || !Stream.CanWrite) return false;
                    Stream.Write(data, 0, data.Length);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error?.Invoke($"TcpSession Send Error: {ex.Message}");
                    Close();
                    OnDisconnected?.Invoke();
                    return false;
                }
            }
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;

            _cts.Cancel();
            try { Stream?.Close(); } catch { }
            try { Client?.Close(); } catch { }
            Stream = null;
            Client = null;
            OnDisconnected?.Invoke();
        }
    }
}
