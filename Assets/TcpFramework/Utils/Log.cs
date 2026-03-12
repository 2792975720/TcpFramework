using System;

namespace TcpFramework
{
    /// <summary>全局日志委托，可在启动时注入（如 Unity 中设为 Debug.Log）。</summary>
    public static class Log
    {
        public static Action<string> Info = Console.WriteLine;
        public static Action<string> Error = Console.WriteLine;
    }
}
