using System;
using System.Collections.Generic;

namespace TcpFramework
{
    /// <summary>
    /// 长度前缀协议： [4 字节长度][2 字节 msgId][payload]
    /// 长度 = 2 + payload.Length（即仅 msgId + payload 的字节数）
    /// </summary>
    public static class LengthPrefixedCodec
    {
        public static byte[] Encode(ushort msgId, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            int totalLen = 2 + payload.Length;
            byte[] buffer = new byte[4 + totalLen];

            Buffer.BlockCopy(BitConverter.GetBytes(totalLen), 0, buffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(msgId), 0, buffer, 4, 2);
            if (payload.Length > 0)
                Buffer.BlockCopy(payload, 0, buffer, 6, payload.Length);

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

        /// <summary>从 buffer 中解码并消费已解析的字节，返回 (msgId, payload) 列表</summary>
        public static IEnumerable<(ushort msgId, byte[] payload)> Decode(PacketBuffer buffer)
        {
            while (buffer.Count >= 4)
            {
                byte[] lenBytes = buffer.Peek(4);
                if (lenBytes == null) break;

                int totalLen = BitConverter.ToInt32(lenBytes, 0);
                if (totalLen < 2 || buffer.Count < 4 + totalLen)
                    break;

                byte[] header = buffer.Peek(4 + totalLen);
                if (header == null) break;

                ushort msgId = BitConverter.ToUInt16(header, 4);
                byte[] payload = new byte[totalLen - 2];
                if (payload.Length > 0)
                    Buffer.BlockCopy(header, 6, payload, 0, payload.Length);

                buffer.Consume(4 + totalLen);
                yield return (msgId, payload);
            }
        }
    }
}
