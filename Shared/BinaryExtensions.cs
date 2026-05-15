// BinaryReader/Writer 확장 — 패킷 직렬화 규칙(문자열, 배열) 적용
using System;
using System.IO;
using System.Text;

namespace Shared
{
    public static class BinaryExtensions
    {
        // [2B UTF8 길이][UTF8 바이트] 형식으로 읽기
        public static string ReadPktString(this BinaryReader reader)
        {
            ushort byteLen = reader.ReadUInt16();
            if (byteLen > PacketConstants.MaxStringLength)
                throw new InvalidDataException($"String length {byteLen} exceeds max {PacketConstants.MaxStringLength}");
            byte[] bytes = reader.ReadBytes(byteLen);
            return Encoding.UTF8.GetString(bytes);
        }

        // [2B UTF8 길이][UTF8 바이트] 형식으로 쓰기
        public static void WritePktString(this BinaryWriter writer, string? value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > PacketConstants.MaxStringLength)
                throw new ArgumentException($"String byte length {bytes.Length} exceeds max {PacketConstants.MaxStringLength}");
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
    }
}
