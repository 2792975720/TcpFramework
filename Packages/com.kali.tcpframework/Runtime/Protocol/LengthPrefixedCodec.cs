using System;
using System.Collections.Generic;

namespace TcpFramework
{
    /// <summary>
    /// 长度前缀协议： [4 字节长度][2 字节 msgId][4 字节 requestId][payload]
    /// 长度 = 6 + payload.Length（即 msgId + requestId + payload 的字节数）
    /// </summary>
    public static class LengthPrefixedCodec
    {
        public static int MaxFrameSize { get; set; } = ProtocolConstants.DefaultMaxFrameSize;

        public static byte[] Encode(ushort msgId, int requestId, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            int totalLen = 6 + payload.Length;
            byte[] buffer = new byte[4 + totalLen];

            Buffer.BlockCopy(BitConverter.GetBytes(totalLen), 0, buffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(msgId), 0, buffer, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(requestId), 0, buffer, 6, 4);
            if (payload.Length > 0)
                Buffer.BlockCopy(payload, 0, buffer, 10, payload.Length);

            return buffer;
        }

        /// <summary>仅长度+body，不包含 msgId（用于兼容旧心跳等）</summary>
        public static byte[] EncodeRaw(byte[] body)
        {
            body ??= Array.Empty<byte>();
            var lenBytes = BitConverter.GetBytes(body.Length);
            var packet = new byte[lenBytes.Length + body.Length];
            Buffer.BlockCopy(lenBytes, 0, packet, 0, lenBytes.Length);
            if (body.Length > 0)
                Buffer.BlockCopy(body, 0, packet, lenBytes.Length, body.Length);
            return packet;
        }

        /// <summary>从 buffer 中解码并消费已解析的字节，返回 (msgId, requestId, payload) 列表</summary>
        public static IEnumerable<(ushort msgId, int requestId, byte[] payload)> Decode(PacketBuffer buffer, Action<int> onInvalidFrame = null)
        {
            while (buffer.Count >= 4)
            {
                byte[] lenBytes = buffer.Peek(4);
                if (lenBytes == null) break;

                int totalLen = BitConverter.ToInt32(lenBytes, 0);
                if (totalLen < 6 || totalLen > MaxFrameSize)
                {
                    onInvalidFrame?.Invoke(totalLen);
                    buffer.Clear();
                    throw new InvalidOperationException($"Invalid frame length: {totalLen}");
                }

                if (buffer.Count < 4 + totalLen)
                    break;

                byte[] header = buffer.Peek(4 + totalLen);
                if (header == null) break;

                ushort msgId = BitConverter.ToUInt16(header, 4);
                int requestId = BitConverter.ToInt32(header, 6);
                byte[] payload = new byte[totalLen - 6];
                if (payload.Length > 0)
                    Buffer.BlockCopy(header, 10, payload, 0, payload.Length);

                buffer.Consume(4 + totalLen);
                yield return (msgId, requestId, payload);
            }
        }
    }
}
