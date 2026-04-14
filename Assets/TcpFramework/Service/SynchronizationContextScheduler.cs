using System;
using System.Threading;

namespace TcpFramework
{
    public sealed class SynchronizationContextScheduler : IMessageScheduler
    {
        private readonly SynchronizationContext _context;
        private readonly int _mainThreadId;

        public SynchronizationContextScheduler(SynchronizationContext context = null)
        {
            _context = context ?? SynchronizationContext.Current ?? new SynchronizationContext();
            _mainThreadId = Environment.CurrentManagedThreadId;
        }

        public bool IsMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

        public void Post(Action action)
        {
            if (action == null) return;
            _context.Post(_ => action(), null);
        }
    }
}
