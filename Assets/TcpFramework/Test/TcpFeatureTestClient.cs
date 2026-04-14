using System;
using TcpFramework;
using UnityEngine;

public class TcpFeatureTestClient : MonoBehaviour
{
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 9000;
    [SerializeField] private bool useJsonSerializer = true;

    private async void Start()
    {
        BindUnityLogOutput();

        var service = TcpService.Instance;
        service.UseScheduler(new SynchronizationContextScheduler());
        service.UseMiddleware(new LoggingMiddleware
        {
            SuccessLevel = LogLevel.Debug,
            LogPayloadBytes = true
        });

        if (useJsonSerializer)
            service.SetSerializer(new JsonMessageSerializer());
        else
            service.SetSerializer(new BinaryMessageSerializer());

        service.Register<TcpFeatureTestMessage>(OnTestMessage);

        var options = new TcpServiceOptions
        {
            Host = host,
            Port = port,
            RpcTimeoutMs = 3000,
            InboundQueueCapacity = 1024,
            QueueOverflowStrategy = QueueOverflowStrategy.DropOldest,
            DispatchOnMainThread = true,
            MaxFrameSize = 1024 * 1024,
            Client = new TcpClientOptions
            {
                ReconnectIntervalMs = 2000,
                HeartbeatIntervalMs = 3000,
                InvalidPacketDisconnectThreshold = 1
            }
        };

        await service.StartAsync(options);
        Debug.Log("[TestClient] Connected. Press Space to send message.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var msg = new TcpFeatureTestMessage
            {
                Text = "hello from test client",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            TcpService.Instance.Send(msg);
            Debug.Log("[TestClient] Send one test message.");
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            var metrics = TcpService.Instance.Metrics;
            Debug.Log($"[Metrics] sent={metrics.SentMessages}, recv={metrics.ReceivedMessages}, queue={metrics.QueueLength}, drop={metrics.DroppedMessages}, reconnect={metrics.ReconnectCount}");
        }
    }

    private void OnDestroy()
    {
        TcpService.Instance.Close();
    }

    private void OnTestMessage(TcpFeatureTestMessage msg)
    {
        Debug.Log($"[TestClient] Received msg text={msg.Text}, ts={msg.Timestamp}");
    }

    private static void BindUnityLogOutput()
    {
        Log.Debug = text => Debug.Log(text);
        Log.Info = text => Debug.Log(text);
        Log.Warn = text => Debug.LogWarning(text);
        Log.Error = text => Debug.LogError(text);
    }
}
