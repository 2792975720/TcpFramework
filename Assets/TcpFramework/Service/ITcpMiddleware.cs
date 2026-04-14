using System;
using System.Threading.Tasks;

namespace TcpFramework
{
    public interface ITcpMiddleware
    {
        Task InvokeAsync(TcpMessageContext context, Func<Task> next);
    }
}
