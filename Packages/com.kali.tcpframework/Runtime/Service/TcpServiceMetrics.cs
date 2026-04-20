using System;
using System.Threading;

namespace TcpFramework
{
    public sealed class TcpServiceMetrics
    {
        private readonly DateTime _startedAtUtc = DateTime.UtcNow;
        private long _reconnectCount;
        private long _sentMessages;
        private long _receivedMessages;
        private long _sentBytes;
        private long _receivedBytes;
        private long _droppedMessages;
        private DateTime _connectedAtUtc;
        private DateTime _lastSampleAtUtc = DateTime.UtcNow;
        private long _lastSentMessages;
        private long _lastReceivedMessages;

        public TimeSpan Uptime => DateTime.UtcNow - _startedAtUtc;
        public TimeSpan ConnectedDuration => _connectedAtUtc == default ? TimeSpan.Zero : DateTime.UtcNow - _connectedAtUtc;
        public long ReconnectCount => Interlocked.Read(ref _reconnectCount);
        public long SentMessages => Interlocked.Read(ref _sentMessages);
        public long ReceivedMessages => Interlocked.Read(ref _receivedMessages);
        public long SentBytes => Interlocked.Read(ref _sentBytes);
        public long ReceivedBytes => Interlocked.Read(ref _receivedBytes);
        public long DroppedMessages => Interlocked.Read(ref _droppedMessages);
        public double SendRatePerSecond { get; private set; }
        public double ReceiveRatePerSecond { get; private set; }
        public int QueueLength { get; internal set; }
        public long QueueDroppedCount { get; internal set; }
        public int InvalidPacketCount { get; internal set; }

        internal void MarkConnected()
        {
            _connectedAtUtc = DateTime.UtcNow;
        }

        internal void IncrementReconnect()
        {
            Interlocked.Increment(ref _reconnectCount);
        }

        internal void AddSent(int bytes)
        {
            Interlocked.Increment(ref _sentMessages);
            Interlocked.Add(ref _sentBytes, bytes);
        }

        internal void AddReceived(int bytes)
        {
            Interlocked.Increment(ref _receivedMessages);
            Interlocked.Add(ref _receivedBytes, bytes);
        }

        internal void IncrementDropped()
        {
            Interlocked.Increment(ref _droppedMessages);
        }

        internal void SampleRates()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastSampleAtUtc).TotalSeconds;
            if (elapsed <= 0) return;

            long sent = SentMessages;
            long received = ReceivedMessages;
            SendRatePerSecond = (sent - _lastSentMessages) / elapsed;
            ReceiveRatePerSecond = (received - _lastReceivedMessages) / elapsed;
            _lastSentMessages = sent;
            _lastReceivedMessages = received;
            _lastSampleAtUtc = now;
        }
    }
}
