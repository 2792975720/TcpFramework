using System;
using System.Threading;
using System.Threading.Tasks;

namespace TcpFramework
{
    /// <summary>定时发送心跳、记录最后接收时间，用于保活与超时检测。</summary>
    public class HeartbeatManager
    {
        private readonly Action _sendHeartbeat;
        private int _intervalMs;
        private Task _loopTask;

        public DateTime LastReceiveTime { get; private set; } = DateTime.Now;

        public HeartbeatManager(Action send, int intervalMs = 3000)
        {
            _sendHeartbeat = send ?? throw new ArgumentNullException(nameof(send));
            _intervalMs = Math.Max(100, intervalMs);
        }

        public void Start(CancellationToken token)
        {
            _loopTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(Volatile.Read(ref _intervalMs), token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) break;
                        _sendHeartbeat();
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        public void Stop()
        {
            _loopTask = null;
        }

        public void UpdateInterval(int intervalMs)
        {
            Interlocked.Exchange(ref _intervalMs, Math.Max(100, intervalMs));
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
