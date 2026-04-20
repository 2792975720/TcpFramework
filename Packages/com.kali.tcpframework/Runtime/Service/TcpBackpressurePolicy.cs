namespace TcpFramework
{
    public sealed class TcpBackpressurePolicy
    {
        public bool DropWhenQueueBusy { get; set; }
        public int QueueBusyThreshold { get; set; } = 1024;
    }
}
