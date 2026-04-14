using System.Text;
using TcpFramework;
using UnityEngine;

public class TcpFeatureTestServer : MonoBehaviour
{
    [SerializeField] private int port = 9000;
    private TcpServerEx _server;

    private void Start()
    {
        _server = new TcpServerEx();
        _server.OnClientConnected += session =>
        {
            Debug.Log($"[TestServer] Client connected: {session.Client?.Client?.RemoteEndPoint}");
        };
        _server.OnClientMessage += (session, msgId, payload) =>
        {
            var text = payload == null ? string.Empty : Encoding.UTF8.GetString(payload);
            Debug.Log($"[TestServer] Recv msgId={msgId}, text={text}");

            // 回包给客户端，验证消息链路（这里只做简单回显）
            session.Send(msgId, 0, payload ?? System.Array.Empty<byte>());
        };

        _server.Start(port);
        Debug.Log($"[TestServer] Started on {port}");
    }

    private void OnDestroy()
    {
        _server?.Stop();
    }
}
