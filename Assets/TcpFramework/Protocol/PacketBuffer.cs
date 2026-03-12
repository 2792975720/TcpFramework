using System;
using System.Collections.Generic;

namespace TcpFramework
{
    /// <summary>
    /// 单一缓冲：只使用 List&lt;byte&gt;，供长度前缀协议写入与解码消费。
    /// </summary>
    public class PacketBuffer
    {
        private readonly List<byte> _data = new List<byte>();

        public int Count => _data.Count;

        public void Write(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            _data.AddRange(data);
        }

        public void Write(byte[] data, int offset, int length)
        {
            if (data == null || length <= 0) return;
            for (int i = 0; i < length; i++)
                _data.Add(data[offset + i]);
        }

        /// <summary>查看前 count 字节（不消费）</summary>
        public byte[] Peek(int count)
        {
            if (count <= 0 || _data.Count < count) return null;
            var result = new byte[count];
            for (int i = 0; i < count; i++)
                result[i] = _data[i];
            return result;
        }

        /// <summary>从头部消费 count 字节</summary>
        public void Consume(int count)
        {
            if (count <= 0) return;
            if (count >= _data.Count)
            {
                _data.Clear();
                return;
            }
            _data.RemoveRange(0, count);
        }

        public void Clear()
        {
            _data.Clear();
        }
    }
}
