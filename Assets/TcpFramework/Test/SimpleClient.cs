using System.Text;
using UnityEngine;
using TcpFramework;

public class SimpleClient : MonoBehaviour
{
    private TcpClientEx _client;

    private async void Start()
    {
        _client = new TcpClientEx();

        _client.OnConnected += () => Debug.Log("[Client] Connected");
        _client.OnDisconnected += () => Debug.Log("[Client] Disconnected");

        _client.OnMessage += (msgId, payload) =>
        {
            string text = Encoding.UTF8.GetString(payload);
            Debug.Log($"[Client] Received ({msgId}): {text}");
        };

        await _client.ConnectAsync("127.0.0.1", 9000);
    }

    private void Update()
    {
        // 按空格发送一条测试消息
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _client.SendString("Hello server!"); // 默认 msgId = 1
        }
    }

    private void OnDestroy()
    {
        _client?.Close();
    }
}