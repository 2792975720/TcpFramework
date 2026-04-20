using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TcpFramework
{
    public enum QueueOverflowStrategy
    {
        DropNewest,
        DropOldest
    }

    /// <summary>对 <see cref="ConcurrentQueue{T}"/> 的薄封装，便于扩展。</summary>
    public class ConcurrentQueueEx<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly object _syncRoot = new object();
        private readonly int _maxCapacity;
        private readonly QueueOverflowStrategy _overflowStrategy;
        private long _droppedCount;
        private volatile bool _addingCompleted;

        public int Count => _queue.Count;
        public int MaxCapacity => _maxCapacity;
        public long DroppedCount => Interlocked.Read(ref _droppedCount);
        public bool IsAddingCompleted => _addingCompleted;
        public bool IsCompleted => _addingCompleted && _queue.IsEmpty;

        public ConcurrentQueueEx(int maxCapacity = 0, QueueOverflowStrategy overflowStrategy = QueueOverflowStrategy.DropOldest)
        {
            if (maxCapacity < 0) throw new ArgumentOutOfRangeException(nameof(maxCapacity));
            _maxCapacity = maxCapacity;
            _overflowStrategy = overflowStrategy;
        }

        public void Enqueue(T item)
        {
            TryEnqueue(item);
        }

        public bool TryEnqueue(T item)
        {
            lock (_syncRoot)
            {
                if (_addingCompleted) return false;

                if (_maxCapacity > 0 && _queue.Count >= _maxCapacity)
                {
                    if (_overflowStrategy == QueueOverflowStrategy.DropNewest)
                    {
                        Interlocked.Increment(ref _droppedCount);
                        return false;
                    }

                    if (_queue.TryDequeue(out _))
                    {
                        // 队列信号与元素数量保持大致一致，防止等待误唤醒后长时间空转。
                        _signal.Wait(0);
                        Interlocked.Increment(ref _droppedCount);
                    }
                }

                _queue.Enqueue(item);
                _signal.Release();
                return true;
            }
        }

        public int EnqueueRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            int count = 0;
            foreach (var item in items)
            {
                if (TryEnqueue(item)) count++;
            }
            return count;
        }

        public bool TryDequeue(out T item)
        {
            if (_queue.TryDequeue(out item))
            {
                _signal.Wait(0);
                return true;
            }
            return false;
        }

        public int TryDequeueBatch(int maxCount, List<T> buffer)
        {
            if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            int count = 0;
            while (count < maxCount && _queue.TryDequeue(out var item))
            {
                buffer.Add(item);
                _signal.Wait(0);
                count++;
            }
            return count;
        }

        public bool TryPeek(out T item) => _queue.TryPeek(out item);

        public T[] ToArray() => _queue.ToArray();

        public T WaitDequeue(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (_addingCompleted && _queue.IsEmpty)
                    throw new InvalidOperationException("Queue has been completed.");

                _signal.Wait(cancellationToken);
                if (_queue.TryDequeue(out var item)) return item;
            }
        }

        public async Task<T> WaitDequeueAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (_addingCompleted && _queue.IsEmpty)
                    throw new InvalidOperationException("Queue has been completed.");

                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (_queue.TryDequeue(out var item)) return item;
            }
        }

        public void Complete()
        {
            _addingCompleted = true;
            _signal.Release();
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                while (_queue.TryDequeue(out _)) { }
                while (_signal.Wait(0)) { }
            }
        }
    }
}
