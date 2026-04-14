using System;
using System.Collections.Generic;
using System.Text;

namespace TcpFramework
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public sealed class LogEvent
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>统一日志入口：分级 + 结构化字段，保留兼容委托。</summary>
    public static class Log
    {
        public static Action<string> Info = Console.WriteLine;
        public static Action<string> Error = Console.WriteLine;
        public static Action<string> Debug = _ => { };
        public static Action<string> Warn = Console.WriteLine;
        public static Action<LogEvent> Writer { get; set; }
        public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        public static Dictionary<string, object> Fields(params (string key, object value)[] fields)
        {
            var result = new Dictionary<string, object>();
            if (fields == null) return result;
            foreach (var (key, value) in fields)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = value;
            }
            return result;
        }

        public static void Write(LogLevel level, string message, Dictionary<string, object> fields = null, Exception exception = null)
        {
            if (level < MinimumLevel) return;

            var evt = new LogEvent
            {
                Level = level,
                Message = message ?? string.Empty,
                Fields = fields ?? new Dictionary<string, object>(),
                Exception = exception
            };
            Writer?.Invoke(evt);

            var text = Format(evt);
            switch (level)
            {
                case LogLevel.Debug:
                    Debug?.Invoke(text);
                    break;
                case LogLevel.Info:
                    Info?.Invoke(text);
                    break;
                case LogLevel.Warn:
                    Warn?.Invoke(text);
                    break;
                default:
                    Error?.Invoke(text);
                    break;
            }
        }

        private static string Format(LogEvent evt)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(evt.Level).Append("] ").Append(evt.Message ?? string.Empty);
            if (evt.Fields != null && evt.Fields.Count > 0)
            {
                sb.Append(" | ");
                bool first = true;
                foreach (var kv in evt.Fields)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(kv.Key).Append('=').Append(kv.Value);
                }
            }

            if (evt.Exception != null)
            {
                sb.Append(" | ex=").Append(evt.Exception.GetType().Name);
                if (!string.IsNullOrEmpty(evt.Exception.Message))
                    sb.Append(':').Append(evt.Exception.Message);
            }

            return sb.ToString();
        }
    }
}
