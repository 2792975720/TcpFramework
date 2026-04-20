# TcpFramework — 通用 TCP 网络通信模块

## 作为 UPM 包给其他 Unity 工程使用

- **包名**：`com.kali.tcpframework`
- **最低 Unity**：2019.4.12f1（`package.json` 中 `unity` + `unityRelease`）
- **本仓库内嵌路径**：`Packages/com.kali.tcpframework/`

在其他工程的 `Packages/manifest.json` 的 `dependencies` 里增加（把 URL 换成你的仓库地址，并用 **tag/commit** 固定版本）：

```json
"com.kali.tcpframework": "https://github.com/<你的账号>/TcpFramework.git?path=/Packages/com.kali.tcpframework#v1.0.0"
```

拉取后代码位于包内 `Runtime/`，程序集名为 **Kali.TcpFramework**。

## 结构

- **Core**： TcpServerEx （服务端）、 TcpClientEx （客户端）、 TcpSession （单连接）
- **Protocol**： LengthPrefixedCodec （长度前缀编解码）、 PacketBuffer 、 IMessage 、 ProtocolConstants （如心跳 MsgId）、IMessageSerializer、BinaryMessageSerializer、JsonMessageSerializer
- **Service**： MessageDispatcher （按  IMessage  类型注册/派发）、 TcpService （客户端门面）、ITcpMiddleware、LoggingMiddleware、TcpServiceOptions、TcpServiceMetrics、TcpConnectionPool、IMessageScheduler
- **Heartbeat**： HeartbeatManager  保活与超时
- **Utils**： Log （可注入）、 ConcurrentQueueEx 、 TcpEndpointParser （从网址解析 host:port）
- **Test**：示例与联调脚本（ TcpFeatureTest* 等）
- **Docs**：说明文档（本文件）

## 协议格式

- 包结构： [4 字节长度][2 字节 msgId][4 字节 requestId][payload] ，长度 = 6 + payload 字节数
- 心跳：msgId =  ProtocolConstants.HeartbeatMsgId （0），payload 可为空

## 使用说明

1. **服务端**：new TcpServerEx() → Start(port)，订阅 OnClientConnected / OnClientMessage，用 Broadcast 或  session.Send  回包。
2. **客户端**： new TcpClientEx()  →  ConnectAsync(host, port) ，订阅  OnMessage ，用  Send(msgId, payload)  或  SendString  发送。 host  可为 IP 或域名（如  "game.example.com" ）；若使用完整地址字符串（如  "tcp://game.example.com:9000" ），可用  TcpEndpointParser.Parse(address)  得到  (host, port)  再传入。
3. **基于 IMessage**：用  TcpService.Instance  或自建  MessageDispatcher ， Register<T>(handler)  后收到对应 MsgId 会反序列化并回调。
4. **RPC**：`SendAsync<TReq,TResp>(timeout)` 支持 requestId 关联响应与超时控制。
5. **中间件**：`UseMiddleware(new LoggingMiddleware())` 可挂入站/出站拦截链路。
6. **序列化**：`SetSerializer(new JsonMessageSerializer())` 可切换 JSON 策略。
7. **配置**：`StartAsync(TcpServiceOptions)` + `UpdateOptions(...)` 支持运行时更新 host/port/重连/心跳/超时/队列容量。
8. **多连接**：`TcpConnectionPool` 支持按 key 管理多路连接（聊天/战斗/同步分流）。
9. **主线程桥接**：`DispatchOnMainThread=true` + `UseScheduler(new SynchronizationContextScheduler())` 可把消息派发切回主线程。

## 日志

- 统一入口：`Log.Write(LogLevel, message, fields, exception)`，支持 `Debug/Info/Warn/Error` 分级和结构化字段。
- 常见字段：`serviceId`、`connectionId`、`msgId`、`requestId`、`elapsedMs`、`payloadBytes`、`queueLength`、`poolKey`。
- 新模块已接入日志：
  - `TcpService`：配置更新、序列化器切换、中间件注册、背压策略、连接状态、RPC 超时、关闭。
  - `TcpConnectionPool`：连接启动/关闭/全部关闭。
  - `LoggingMiddleware`：入站/出站消息耗时与异常日志。
  - `JsonMessageSerializer`：序列化/反序列化失败日志。
