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
        private readonly CancellationTokenSource _cts;
        private readonly int _invalidPacketDisconnectThreshold;
        private int _invalidPacketCount;
        private int _disconnectedNotified;
        private readonly string _connectionId;

        public event Action<ushort, int, byte[]> OnMessage;
        public event Action OnDisconnected;
        public event Action<int> OnInvalidPacket;

        public bool Connected => Client != null && Client.Connected;

        public int InvalidPacketCount => Volatile.Read(ref _invalidPacketCount);

        public TcpSession(TcpClient client, CancellationToken lifetimeToken = default, int invalidPacketDisconnectThreshold = ProtocolConstants.DefaultInvalidPacketDisconnectThreshold)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Stream = client.GetStream();
            _invalidPacketDisconnectThreshold = Math.Max(1, invalidPacketDisconnectThreshold);
            _connectionId = client.Client?.RemoteEndPoint?.ToString() ?? "unknown";
            _cts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
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

                    foreach (var (msgId, requestId, payload) in LengthPrefixedCodec.Decode(Buffer, OnInvalidFrame))
                        OnMessage?.Invoke(msgId, requestId, payload);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* 断开或异常，忽略 */ }
            finally
            {
                Close();
                NotifyDisconnectedOnce();
            }
        }

        private void OnInvalidFrame(int frameLength)
        {
            int count = Interlocked.Increment(ref _invalidPacketCount);
            OnInvalidPacket?.Invoke(frameLength);
            Log.Write(LogLevel.Warn, "Invalid frame length detected.",
                Log.Fields(("connectionId", _connectionId), ("frameLength", frameLength), ("count", count)));
            if (count >= _invalidPacketDisconnectThreshold)
                Close();
        }

        public bool Send(ushort msgId, int requestId, byte[] payload)
        {
            byte[] packet = LengthPrefixedCodec.Encode(msgId, requestId, payload);
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
                    Log.Write(LogLevel.Error, "TcpSession send failed.",
                        Log.Fields(("connectionId", _connectionId), ("bytes", data.Length)), ex);
                    Close();
                    NotifyDisconnectedOnce();
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
            NotifyDisconnectedOnce();
        }

        private void NotifyDisconnectedOnce()
        {
            if (Interlocked.Exchange(ref _disconnectedNotified, 1) == 1) return;
            OnDisconnected?.Invoke();
        }
    }
}
