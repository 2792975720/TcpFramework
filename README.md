# TcpFramework — 通用 TCP 网络通信模块

## 结构

- **Core**：`TcpServerEx`（服务端）、`TcpClientEx`（客户端）、`TcpSession`（单连接）
- **Protocol**：`LengthPrefixedCodec`（长度前缀编解码）、`PacketBuffer`、`IMessage`、`ProtocolConstants`（如心跳 MsgId）
- **Service**：`MessageDispatcher`（按 `IMessage` 类型注册/派发）、`TcpService`（单例客户端门面，配合 `Register<T>()` 使用）
- **Heartbeat**：`HeartbeatManager` 保活与超时
- **Utils**：`Log`（可注入）、`ConcurrentQueueEx`、`TcpEndpointParser`（从网址解析 host:port）
- **Test**：简单示例（`SimpleServer`、`SimpleClient`）
- **Docs**：说明文档（本文件）

## 协议格式

- 包结构：`[4 字节长度][2 字节 msgId][payload]`，长度 = 2 + payload 字节数
- 心跳：msgId = `ProtocolConstants.HeartbeatMsgId`（0），payload 可为空

## 使用说明

1. **服务端**：`new TcpServerEx()` → `Start(port)`，订阅 `OnClientConnected` / `OnClientMessage`，用 `Broadcast` 或 `session.Send` 回包。
2. **客户端**：`new TcpClientEx()` → `ConnectAsync(host, port)`，订阅 `OnMessage`，用 `Send(msgId, payload)` 或 `SendString` 发送。`host` 可为 IP 或域名（如 `"game.example.com"`）；若使用完整地址字符串（如 `"tcp://game.example.com:9000"`），可用 `TcpEndpointParser.Parse(address)` 得到 `(host, port)` 再传入。
3. **基于 IMessage**：用 `TcpService.Instance` 或自建 `MessageDispatcher`，`Register<T>(handler)` 后收到对应 MsgId 会反序列化并回调。
4. **线程**：`OnMessage` / `OnClientMessage` 等可能在后台线程触发，Unity 中请通过主线程派发再操作 UI。

## 日志

在启动时设置 `TcpFramework.Log.Info` / `TcpFramework.Log.Error`（例如 Unity 中设为 `Debug.Log` / `Debug.LogError`）。
