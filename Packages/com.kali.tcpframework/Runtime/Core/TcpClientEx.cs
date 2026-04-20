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
        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Reconnecting,
            Closing
        }

        public TcpSession Session { get; private set; }
        public HeartbeatManager Heartbeat { get; private set; }

        private string _host;
        private int _port;
        private bool _autoReconnect = true;
        private readonly object _stateLock = new object();
        private CancellationTokenSource _lifecycleCts;
        private Task _connectLoopTask;
        private int _invalidPacketCount;
        private ConnectionState _state = ConnectionState.Disconnected;
        private TcpClientOptions _options = new TcpClientOptions();
        private string _connectionId = Guid.NewGuid().ToString("N");

        public event Action<ushort, int, byte[]> OnMessage;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<ushort, Exception> OnMessageDispatchError;

        public ConnectionState State
        {
            get
            {
                lock (_stateLock) return _state;
            }
        }

        public int InvalidPacketCount => Volatile.Read(ref _invalidPacketCount);
        public string ConnectionId => _connectionId;

        public void UpdateOptions(TcpClientOptions options)
        {
            if (options == null) return;
            _options = options;
            Heartbeat?.UpdateInterval(_options.HeartbeatIntervalMs);
        }

        public async Task ConnectAsync(string host, int port, CancellationToken token = default)
        {
            lock (_stateLock)
            {
                if (_state != ConnectionState.Disconnected)
                    throw new InvalidOperationException($"ConnectAsync is not allowed in state {_state}.");
                _state = ConnectionState.Connecting;
            }

            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _connectionId = $"{_host}:{_port}-{Guid.NewGuid():N}";
            _autoReconnect = true;
            _invalidPacketCount = 0;
            _lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _connectLoopTask = Task.Run(() => ConnectLoopAsync(initialConnect: true, _lifecycleCts.Token));
            await _connectLoopTask.ConfigureAwait(false);
        }

        public void Send(ushort msgId, byte[] payload, int requestId = 0)
        {
            byte[] packet = LengthPrefixedCodec.Encode(msgId, requestId, payload ?? Array.Empty<byte>());
            Session?.Send(packet);
        }

        /// <summary>发送原始 body（仅 4 字节长度 + body，无 msgId），一般用于兼容旧逻辑</summary>
        public void SendRaw(byte[] body)
        {
            byte[] packet = LengthPrefixedCodec.EncodeRaw(body ?? Array.Empty<byte>());
            Session?.Send(packet);
        }

        private async Task ConnectLoopAsync(bool initialConnect, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = new TcpClient();
                    await client.ConnectAsync(_host, _port).ConfigureAwait(false);

                    BindSession(new TcpSession(client, token, _options.InvalidPacketDisconnectThreshold));
                    StartHeartbeat();

                    lock (_stateLock)
                        _state = ConnectionState.Connected;

                    SafeInvoke(() => OnConnected?.Invoke());
                    return;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) return;
                    Log.Write(LogLevel.Warn, "Connect failed, will retry.",
                        Log.Fields(("connectionId", _connectionId), ("host", _host), ("port", _port), ("state", State.ToString())), ex);
                    if (!_autoReconnect && !initialConnect)
                        break;
                    try
                    {
                        await Task.Delay(Math.Max(100, _options.ReconnectIntervalMs), token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }

            lock (_stateLock)
            {
                if (_state != ConnectionState.Closing)
                    _state = ConnectionState.Disconnected;
            }
        }

        private void BindSession(TcpSession session)
        {
            Session = session;
            Session.OnMessage += (msgId, requestId, payload) =>
            {
                Heartbeat?.Refresh();
                try
                {
                    OnMessage?.Invoke(msgId, requestId, payload);
                }
                catch (Exception ex)
                {
                    OnMessageDispatchError?.Invoke(msgId, ex);
                    Log.Write(LogLevel.Error, "OnMessage callback failed.",
                        Log.Fields(("connectionId", _connectionId), ("msgId", msgId), ("requestId", requestId)), ex);
                }
            };
            Session.OnInvalidPacket += _ => Interlocked.Increment(ref _invalidPacketCount);
            Session.OnDisconnected += HandleDisconnect;
        }

        private void StartHeartbeat()
        {
            Heartbeat = new HeartbeatManager(() =>
            {
                Send(ProtocolConstants.HeartbeatMsgId, Array.Empty<byte>(), 0);
            }, _options.HeartbeatIntervalMs);
            Heartbeat.Start(_lifecycleCts.Token);
        }

        private void HandleDisconnect()
        {
            if (_lifecycleCts == null || _lifecycleCts.IsCancellationRequested) return;

            lock (_stateLock)
            {
                if (_state == ConnectionState.Closing || _state == ConnectionState.Disconnected)
                    return;
                if (_state == ConnectionState.Reconnecting)
                    return;
                _state = ConnectionState.Reconnecting;
            }

            SafeInvoke(() => OnDisconnected?.Invoke());
            Heartbeat?.Stop();
            Heartbeat = null;
            Session = null;

            if (_autoReconnect)
                _ = Task.Run(() => ConnectLoopAsync(initialConnect: false, _lifecycleCts.Token));
        }

        public void Close()
        {
            lock (_stateLock)
            {
                if (_state == ConnectionState.Closing || _state == ConnectionState.Disconnected)
                    return;
                _state = ConnectionState.Closing;
            }

            try
            {
                _autoReconnect = false;
                _lifecycleCts?.Cancel();
                Heartbeat?.Stop();
                Heartbeat = null;
                Session?.Close();
                Session = null;
            }
            finally
            {
                _lifecycleCts?.Dispose();
                _lifecycleCts = null;
                lock (_stateLock) _state = ConnectionState.Disconnected;
            }
        }

        /// <summary>发送文本，msgId 默认为 1</summary>
        public void SendString(string msg, ushort msgId = 1)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(msg ?? "");
            Send(msgId, bytes);
        }

        private static void SafeInvoke(Action action)
        {
            try { action?.Invoke(); } catch { }
        }
    }
}
