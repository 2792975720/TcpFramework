using System;
using System.Runtime.Serialization;
using System.Text;
using TcpFramework;

[DataContract]
public class TcpFeatureTestMessage : IMessage
{
    public const ushort Id = 1001;

    [DataMember(Order = 1)]
    public string Text { get; set; }

    [DataMember(Order = 2)]
    public long Timestamp { get; set; }

    public ushort MsgId => Id;

    // 兼容 BinaryMessageSerializer 场景；使用 JsonMessageSerializer 时可忽略这两个方法。
    public byte[] Serialize()
    {
        var text = Text ?? string.Empty;
        return Encoding.UTF8.GetBytes(text);
    }

    public void Deserialize(byte[] data)
    {
        Text = data == null ? string.Empty : Encoding.UTF8.GetString(data);
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
