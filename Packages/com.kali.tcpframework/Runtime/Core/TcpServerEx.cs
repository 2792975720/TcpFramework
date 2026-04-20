using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TcpFramework
{
    /// <summary>
    /// TCP 服务端：监听端口、接受连接、维护会话列表、广播与单发。
    /// 注意：OnClientMessage 可能在后台线程触发，Unity 中需派发到主线程。
    /// </summary>
    public class TcpServerEx
    {
        private TcpListener _listener;
        private readonly List<TcpSession> _sessions = new List<TcpSession>();

        public IReadOnlyList<TcpSession> Sessions => _sessions;

        public event Action<TcpSession> OnClientConnected;
        public event Action<TcpSession, ushort, byte[]> OnClientMessage;

        public void Start(int port)
        {
            Log.Write(LogLevel.Info, "Server start called.", Log.Fields(("port", port)));
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Log.Write(LogLevel.Info, "Server listening.", Log.Fields(("port", port)));
            _ = AcceptLoop();
        }

        public void Broadcast(ushort msgId, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            foreach (var session in _sessions)
                session.Send(msgId, 0, payload);
        }

        private async Task AcceptLoop()
        {
            while (_listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    var session = new TcpSession(client);
                    _sessions.Add(session);

                    OnClientConnected?.Invoke(session);

                    session.OnMessage += (msgId, requestId, data) =>
                    {
                        OnClientMessage?.Invoke(session, msgId, data);
                    };

                    session.OnDisconnected += () =>
                    {
                        _sessions.Remove(session);
                    };
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Log.Write(LogLevel.Error, "AcceptLoop failed.", exception: ex);
                }
            }
        }

        public void Stop()
        {
            foreach (var s in _sessions.ToArray())
                s.Close();
            _sessions.Clear();
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }
    }
}
