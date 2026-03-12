using System.Collections.Concurrent;

namespace TcpFramework
{
    /// <summary>对 <see cref="ConcurrentQueue{T}"/> 的薄封装，便于扩展。</summary>
    public class ConcurrentQueueEx<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();

        public int Count => _queue.Count;

        public void Enqueue(T item) => _queue.Enqueue(item);

        public bool TryDequeue(out T item) => _queue.TryDequeue(out item);

        public void Clear()
        {
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
