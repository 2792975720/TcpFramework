using System.Text;
using UnityEngine;
using TcpFramework;


    public class SimpleServer : MonoBehaviour
    {
        private TcpServerEx _server;

        private void Start()
        {
            // 1. 启动服务器
            _server = new TcpServerEx();
            _server.OnClientConnected += session =>
            {
                Debug.Log($"[Server] Client connected: {session.Client.Client.RemoteEndPoint}");
            };
            _server.OnClientMessage += (session, msgId, payload) =>
            {
                // 注意：这里在后台线程，Unity 操作要派发到主线程
                string text = Encoding.UTF8.GetString(payload);
                Debug.Log($"[Server] Received ({msgId}): {text}");
                // 回复客户端
                var bytes = Encoding.UTF8.GetBytes("Hello from server");
                session.Send(1, bytes);
            };
            _server.Start(9000);
        }

        private void OnDestroy()
        {
            _server?.Stop();
        }
    }
