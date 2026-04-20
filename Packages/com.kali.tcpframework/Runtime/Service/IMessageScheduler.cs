using System;

namespace TcpFramework
{
    public interface IMessageScheduler
    {
        bool IsMainThread { get; }
        void Post(Action action);
    }
}
