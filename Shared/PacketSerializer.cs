// 패킷 헤더 조립(Build) 및 수신 스트림 파싱(TryParse)
using System;
using System.IO;
using System.Text;

namespace Shared
{
    public static class PacketSerializer
    {
        // 패킷 ID + 페이로드 액션 → 헤더 포함 완성 패킷 바이트 배열
        public static byte[] Build(PacketId id, Action<BinaryWriter>? writePayload = null)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            writer.Write(PacketConstants.MagicNumber); // 2B magic
            writer.Write((ushort)0);                   // 2B length (나중에 채움)
            writer.Write((ushort)id);                  // 2B packet id

            writePayload?.Invoke(writer);
            writer.Flush();

            byte[] bytes = ms.ToArray();

            // Length 필드 채우기 (little-endian, 전체 패킷 크기)
            ushort length = (ushort)bytes.Length;
            bytes[2] = (byte)(length & 0xFF);
            bytes[3] = (byte)(length >> 8);

            return bytes;
        }

        // 버퍼에서 완성 패킷 하나를 파싱 시도
        // true → 파싱 성공, false → 데이터 부족 (더 받아야 함)
        // InvalidDataException → Magic 불일치 or 크기 위반 → 연결 종료 대상
        public static bool TryParse(
            byte[] buffer, int offset, int available,
            out int packetLength, out PacketId packetId, out byte[] payload)
        {
            packetLength = 0;
            packetId    = default;
            payload     = Array.Empty<byte>();

            if (available < PacketConstants.HeaderSize)
                return false;

            ushort magic = BitConverter.ToUInt16(buffer, offset);
            if (magic != PacketConstants.MagicNumber)
                throw new InvalidDataException($"Bad magic: 0x{magic:X4}");

            ushort length = BitConverter.ToUInt16(buffer, offset + 2);
            if (length < PacketConstants.HeaderSize || length > PacketConstants.MaxPacketSize)
                throw new InvalidDataException($"Invalid packet length: {length}");

            if (available < length)
                return false;

            packetId    = (PacketId)BitConverter.ToUInt16(buffer, offset + 4);
            packetLength = length;

            int payloadLen = length - PacketConstants.HeaderSize;
            payload = new byte[payloadLen];
            if (payloadLen > 0)
                Array.Copy(buffer, offset + PacketConstants.HeaderSize, payload, 0, payloadLen);

            return true;
        }
    }
}
