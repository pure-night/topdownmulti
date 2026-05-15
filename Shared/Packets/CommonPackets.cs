// 공용 패킷: 에러 응답, 하트비트
using System.IO;

namespace Shared.Packets
{
    public class S_ERROR
    {
        public string Message = string.Empty;

        public static S_ERROR Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_ERROR { Message = r.ReadPktString() };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_ERROR,
            w => w.WritePktString(Message));
    }

    public class C_HEARTBEAT
    {
        public static C_HEARTBEAT Deserialize(byte[] payload) => new C_HEARTBEAT();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_HEARTBEAT);
    }

    public class S_HEARTBEAT_ACK
    {
        public static S_HEARTBEAT_ACK Deserialize(byte[] payload) => new S_HEARTBEAT_ACK();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_HEARTBEAT_ACK);
    }
}
