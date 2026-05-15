// 패킷 프로토콜 상수
namespace Shared
{
    public static class PacketConstants
    {
        public const ushort MagicNumber    = 0xC0DE;
        public const int    MaxPacketSize  = 4096;
        public const int    MaxStringLength = 32;
        public const int    MaxArrayLength  = 64;
        public const int    HeaderSize      = 6; // Magic(2) + Length(2) + PacketId(2)
    }
}
