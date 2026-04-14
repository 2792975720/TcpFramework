using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TcpFramework
{
    public sealed class LoggingMiddleware : ITcpMiddleware
    {
        public LogLevel SuccessLevel { get; set; } = LogLevel.Debug;
        public bool LogPayloadBytes { get; set; } = true;

        public async Task InvokeAsync(TcpMessageContext context, Func<Task> next)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (next == null) throw new ArgumentNullException(nameof(next));

            var sw = Stopwatch.StartNew();
            try
            {
                await next().ConfigureAwait(false);
                sw.Stop();
                Log.Write(SuccessLevel, "Tcp message processed.",
                    BuildFields(context, sw.ElapsedMilliseconds, null));
            }
            catch (Exception ex)
            {
                sw.Stop();
                Log.Write(LogLevel.Error, "Tcp message processing failed.",
                    BuildFields(context, sw.ElapsedMilliseconds, ex), ex);
                throw;
            }
        }

        private Dictionary<string, object> BuildFields(TcpMessageContext context, long elapsedMs, Exception ex)
        {
            var fields = Log.Fields(
                ("direction", context.Direction.ToString()),
                ("msgId", context.MsgId),
                ("requestId", context.RequestId),
                ("elapsedMs", elapsedMs));

            if (LogPayloadBytes)
                fields["payloadBytes"] = context.Payload?.Length ?? 0;

            if (context.Items != null && context.Items.Count > 0)
                fields["contextItems"] = context.Items.Count;

            if (ex != null)
                fields["exceptionType"] = ex.GetType().Name;

            return fields;
        }
    }
}
