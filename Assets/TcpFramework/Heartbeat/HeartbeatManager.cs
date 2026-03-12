using System;
using System.Threading;

namespace TcpFramework
{
    /// <summary>定时发送心跳、记录最后接收时间，用于保活与超时检测。</summary>
    public class HeartbeatManager
    {
        private readonly Action _sendHeartbeat;
        private readonly int _intervalMs;
        private Timer _timer;

        public DateTime LastReceiveTime { get; private set; } = DateTime.Now;

        public HeartbeatManager(Action send, int intervalMs = 3000)
        {
            _sendHeartbeat = send ?? throw new ArgumentNullException(nameof(send));
            _intervalMs = intervalMs;
        }

        public void Start()
        {
            _timer = new Timer(_ => _sendHeartbeat(), null, _intervalMs, _intervalMs);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Refresh()
        {
            LastReceiveTime = DateTime.Now;
        }

        public bool IsTimeout(int timeoutMs)
        {
            return (DateTime.Now - LastReceiveTime).TotalMilliseconds > timeoutMs;
        }
    }
}
