using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TcpFramework
{
    /// <summary>
    /// 单例形式的 TCP 客户端门面：连接 + 基于 IMessage 的消息派发。
    /// 适合简单客户端场景，业务通过 Register&lt;T&gt; 注册消息处理。
    /// </summary>
    public class TcpService
    {
        public static TcpService Instance { get; } = new TcpService();

        private TcpClientEx _client;
        private IMessageSerializer _serializer = new BinaryMessageSerializer();
        private MessageDispatcher _dispatcher;
        private ConcurrentQueueEx<(ushort msgId, int requestId, byte[] payload)> _messageQueue = new ConcurrentQueueEx<(ushort, int, byte[])>();
        private CancellationTokenSource _dispatchCts;
        private Task _dispatchTask;
        private CancellationTokenSource _serviceCts;
        private readonly List<ITcpMiddleware> _middlewares = new List<ITcpMiddleware>();
        private readonly Dictionary<int, TaskCompletionSource<(ushort msgId, byte[] payload)>> _pendingRequests =
            new Dictionary<int, TaskCompletionSource<(ushort, byte[])>>();
        private readonly Dictionary<ushort, TcpBackpressurePolicy> _backpressurePolicies = new Dictionary<ushort, TcpBackpressurePolicy>();
        private readonly object _pendingLock = new object();
        private int _requestIdSeed;
        private int _lastClientState = (int)TcpClientEx.ConnectionState.Disconnected;
        private readonly Stopwatch _metricsSampleWatch = Stopwatch.StartNew();
        private TcpServiceOptions _options = new TcpServiceOptions();
        private IMessageScheduler _scheduler;
        private readonly string _serviceId = Guid.NewGuid().ToString("N");

        public TcpServiceMetrics Metrics { get; } = new TcpServiceMetrics();
        public TcpServiceOptions Options => _options;

        public bool Connected => _client?.Session != null && _client.Session.Connected;

        public TcpService()
        {
            _dispatcher = new MessageDispatcher(_serializer);
        }

        public void UpdateOptions(TcpServiceOptions options)
        {
            if (options == null) return;
            _options = options;
            if (_options.Client == null) _options.Client = new TcpClientOptions();
            LengthPrefixedCodec.MaxFrameSize = Math.Max(256, _options.MaxFrameSize);
            _client?.UpdateOptions(_options.Client);
            Log.Write(LogLevel.Info, "TcpService options updated.",
                Log.Fields(
                    ("serviceId", _serviceId),
                    ("host", _options.Host),
                    ("port", _options.Port),
                    ("rpcTimeoutMs", _options.RpcTimeoutMs),
                    ("queueCapacity", _options.InboundQueueCapacity),
                    ("queueOverflow", _options.QueueOverflowStrategy.ToString()),
                    ("dispatchOnMainThread", _options.DispatchOnMainThread),
                    ("maxFrameSize", _options.MaxFrameSize),
                    ("reconnectIntervalMs", _options.Client.ReconnectIntervalMs),
                    ("heartbeatIntervalMs", _options.Client.HeartbeatIntervalMs)));
            if (_dispatchCts != null)
            {
                StopDispatchLoop();
                StartDispatchLoop();
            }
        }

        public void UseScheduler(IMessageScheduler scheduler)
        {
            _scheduler = scheduler;
            Log.Write(LogLevel.Info, "TcpService scheduler set.",
                Log.Fields(("serviceId", _serviceId), ("scheduler", scheduler?.GetType().Name ?? "null")));
        }

        public void SetSerializer(IMessageSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _dispatcher = new MessageDispatcher(_serializer)
            {
                OnDispatchError = (msgId, ex) => Log.Write(LogLevel.Error, "Handler failed.",
                    Log.Fields(("serviceId", _serviceId), ("msgId", msgId)), ex)
            };
            Log.Write(LogLevel.Info, "TcpService serializer set.",
                Log.Fields(("serviceId", _serviceId), ("serializer", _serializer.GetType().Name)));
        }

        public void UseMiddleware(ITcpMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _middlewares.Add(middleware);
            Log.Write(LogLevel.Info, "TcpService middleware added.",
                Log.Fields(("serviceId", _serviceId), ("middleware", middleware.GetType().Name), ("middlewareCount", _middlewares.Count)));
        }

        public void ConfigureBackpressure(ushort msgId, TcpBackpressurePolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            _backpressurePolicies[msgId] = policy;
            Log.Write(LogLevel.Info, "Backpressure policy configured.",
                Log.Fields(("serviceId", _serviceId), ("msgId", msgId), ("dropWhenQueueBusy", policy.DropWhenQueueBusy), ("queueBusyThreshold", policy.QueueBusyThreshold)));
        }

        public async Task StartClientAsync(string host, int port)
        {
            var options = new TcpServiceOptions
            {
                Host = host,
                Port = port,
                RpcTimeoutMs = _options.RpcTimeoutMs,
                InboundQueueCapacity = _options.InboundQueueCapacity,
                QueueOverflowStrategy = _options.QueueOverflowStrategy,
                DispatchOnMainThread = _options.DispatchOnMainThread,
                MaxFrameSize = _options.MaxFrameSize,
                Client = _options.Client
            };
            await StartAsync(options).ConfigureAwait(false);
        }

        public async Task StartAsync(TcpServiceOptions options)
        {
            UpdateOptions(options);
            StopDispatchLoop();

            _client = new TcpClientEx();
            _client.UpdateOptions(_options.Client);
            _client.OnMessage += OnRawMessage;
            _client.OnConnected += () =>
            {
                Log.Write(LogLevel.Info, "TcpService connected.",
                    Log.Fields(("serviceId", _serviceId), ("connectionId", _client.ConnectionId), ("host", _options.Host), ("port", _options.Port)));
                Metrics.MarkConnected();
                if (_lastClientState == (int)TcpClientEx.ConnectionState.Reconnecting)
                    Metrics.IncrementReconnect();
                _lastClientState = (int)TcpClientEx.ConnectionState.Connected;
            };
            _client.OnDisconnected += () =>
            {
                Log.Write(LogLevel.Warn, "TcpService disconnected.",
                    Log.Fields(("serviceId", _serviceId), ("connectionId", _client.ConnectionId), ("state", _client.State.ToString())));
                _lastClientState = (int)_client.State;
            };
            _client.OnMessageDispatchError += (msgId, ex) => Log.Write(LogLevel.Error, "OnMessage failed.",
                Log.Fields(("serviceId", _serviceId), ("msgId", msgId), ("connectionId", _client.ConnectionId)), ex);
            _dispatcher.OnDispatchError = (msgId, ex) => Log.Write(LogLevel.Error, "Handler failed.",
                Log.Fields(("serviceId", _serviceId), ("msgId", msgId), ("connectionId", _client.ConnectionId)), ex);

            _serviceCts?.Cancel();
            _serviceCts?.Dispose();
            _serviceCts = new CancellationTokenSource();
            StartDispatchLoop();
            await _client.ConnectAsync(_options.Host, _options.Port, _serviceCts.Token).ConfigureAwait(false);
        }

        public void Send(IMessage msg)
        {
            if (msg == null) return;
            byte[] payload = _serializer.Serialize(msg);
            SendInternalAsync(msg.MsgId, 0, payload).GetAwaiter().GetResult();
        }

        public async Task<TResp> SendAsync<TReq, TResp>(TReq request, int timeoutMs = 0, CancellationToken token = default)
            where TReq : IMessage
            where TResp : IMessage, new()
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (timeoutMs <= 0) timeoutMs = Math.Max(100, _options.RpcTimeoutMs);
            int requestId = Interlocked.Increment(ref _requestIdSeed);
            byte[] payload = _serializer.Serialize(request);
            var tcs = new TaskCompletionSource<(ushort msgId, byte[] payload)>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingLock) _pendingRequests[requestId] = tcs;

            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token, _serviceCts?.Token ?? CancellationToken.None))
            {
                timeoutCts.CancelAfter(timeoutMs);
                using (var reg = timeoutCts.Token.Register(() =>
                {
                    lock (_pendingLock)
                    {
                        if (_pendingRequests.Remove(requestId))
                        {
                            tcs.TrySetException(new TimeoutException($"RPC timeout, requestId={requestId}, msgId={request.MsgId}"));
                            Log.Write(LogLevel.Warn, "RPC timeout.",
                                Log.Fields(("serviceId", _serviceId), ("msgId", request.MsgId), ("requestId", requestId), ("timeoutMs", timeoutMs)));
                        }
                    }
                }))
                {
                    await SendInternalAsync(request.MsgId, requestId, payload).ConfigureAwait(false);
                    var result = await tcs.Task.ConfigureAwait(false);
                    return _serializer.Deserialize<TResp>(result.payload);
                }
            }
        }

        private void OnRawMessage(ushort msgId, int requestId, byte[] payload)
        {
            Metrics.AddReceived(payload?.Length ?? 0);

            lock (_pendingLock)
            {
                if (requestId != 0 && _pendingRequests.TryGetValue(requestId, out var pending))
                {
                    _pendingRequests.Remove(requestId);
                    pending.TrySetResult((msgId, payload ?? Array.Empty<byte>()));
                    return;
                }
            }

            if (ShouldDropInbound(msgId))
            {
                Metrics.IncrementDropped();
                Log.Write(LogLevel.Warn, "Inbound message dropped by backpressure.",
                    Log.Fields(("serviceId", _serviceId), ("msgId", msgId), ("queueLength", _messageQueue.Count)));
                return;
            }

            if (_messageQueue.TryEnqueue((msgId, requestId, payload ?? Array.Empty<byte>())))
            {
                Metrics.QueueLength = _messageQueue.Count;
                Metrics.QueueDroppedCount = _messageQueue.DroppedCount;
            }
        }

        public void Register<T>(Action<T> handler) where T : IMessage, new()
        {
            _dispatcher.Register(handler);
        }

        public void Close()
        {
            _serviceCts?.Cancel();
            _client?.Close();
            StopDispatchLoop();
            lock (_pendingLock)
            {
                foreach (var kv in _pendingRequests)
                    kv.Value.TrySetException(new OperationCanceledException("TcpService closed."));
                _pendingRequests.Clear();
            }
            _serviceCts?.Dispose();
            _serviceCts = null;
            Log.Write(LogLevel.Info, "TcpService closed.",
                Log.Fields(("serviceId", _serviceId), ("pendingRequests", _pendingRequests.Count), ("queueLength", _messageQueue?.Count ?? 0)));
        }

        private void StartDispatchLoop()
        {
            _messageQueue = new ConcurrentQueueEx<(ushort, int, byte[])>(_options.InboundQueueCapacity, _options.QueueOverflowStrategy);
            _dispatchCts = _serviceCts != null
                ? CancellationTokenSource.CreateLinkedTokenSource(_serviceCts.Token)
                : new CancellationTokenSource();
            _dispatchTask = Task.Run(() => DispatchLoopAsync(_dispatchCts.Token));
        }

        private async Task DispatchLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var (msgId, requestId, payload) = await _messageQueue.WaitDequeueAsync(token).ConfigureAwait(false);
                    var context = new TcpMessageContext
                    {
                        MsgId = msgId,
                        RequestId = requestId,
                        Payload = payload,
                        Direction = TcpMessageDirection.Inbound
                    };

                    await RunMiddlewaresAsync(context, () =>
                    {
                        if (_options.DispatchOnMainThread && _scheduler != null && !_scheduler.IsMainThread)
                            return DispatchOnSchedulerAsync(context);

                        _dispatcher.Dispatch(context.MsgId, context.RequestId, context.Payload);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);

                    Metrics.QueueLength = _messageQueue.Count;
                    Metrics.QueueDroppedCount = _messageQueue.DroppedCount;
                    Metrics.InvalidPacketCount = _client?.InvalidPacketCount ?? 0;
                    if (_metricsSampleWatch.ElapsedMilliseconds >= 1000)
                    {
                        Metrics.SampleRates();
                        _metricsSampleWatch.Restart();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { }
        }

        private void StopDispatchLoop()
        {
            if (_dispatchCts == null) return;

            var dispatchTask = _dispatchTask;
            _messageQueue.Complete();
            _dispatchCts.Cancel();

            if (dispatchTask != null)
            {
                try
                {
                    dispatchTask.Wait(TimeSpan.FromMilliseconds(200));
                }
                catch (AggregateException) { }
                catch (OperationCanceledException) { }
            }

            _dispatchCts.Dispose();
            _dispatchCts = null;
            _dispatchTask = null;
        }

        private async Task SendInternalAsync(ushort msgId, int requestId, byte[] payload)
        {
            var context = new TcpMessageContext
            {
                MsgId = msgId,
                RequestId = requestId,
                Payload = payload ?? Array.Empty<byte>(),
                Direction = TcpMessageDirection.Outbound
            };

            await RunMiddlewaresAsync(context, () =>
            {
                _client?.Send(context.MsgId, context.Payload, context.RequestId);
                Metrics.AddSent(context.Payload?.Length ?? 0);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }

        private Task DispatchOnSchedulerAsync(TcpMessageContext context)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _scheduler.Post(() =>
            {
                try
                {
                    _dispatcher.Dispatch(context.MsgId, context.RequestId, context.Payload);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        private async Task RunMiddlewaresAsync(TcpMessageContext context, Func<Task> terminal)
        {
            int index = -1;
            Task Next()
            {
                index++;
                if (index >= _middlewares.Count) return terminal();
                return _middlewares[index].InvokeAsync(context, Next);
            }

            await Next().ConfigureAwait(false);
        }

        private bool ShouldDropInbound(ushort msgId)
        {
            if (!_backpressurePolicies.TryGetValue(msgId, out var policy)) return false;
            if (!policy.DropWhenQueueBusy) return false;
            int threshold = Math.Max(1, policy.QueueBusyThreshold);
            return _messageQueue.Count >= threshold;
        }

    }
}
