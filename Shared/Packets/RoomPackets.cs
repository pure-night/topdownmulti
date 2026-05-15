// 룸 내 흐름 패킷: Ready, 룸 상태, 라운드/매치 시작·종료
using System;
using System.IO;

namespace Shared.Packets
{
    public class C_READY_REQ
    {
        public static C_READY_REQ Deserialize(byte[] payload) => new C_READY_REQ();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_READY_REQ);
    }

    // 룸 상태가 바뀔 때마다 양쪽에 전송
    public class S_ROOM_STATE
    {
        public RoomState RoomState;
        public int    HostPlayerId;
        public string HostNickname  = string.Empty;
        public bool   HostReady;
        public int    GuestPlayerId; // -1 = 없음
        public string GuestNickname = string.Empty;
        public bool   GuestReady;
        public int    HostWins;
        public int    GuestWins;

        public static S_ROOM_STATE Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_ROOM_STATE
            {
                RoomState     = (RoomState)r.ReadByte(),
                HostPlayerId  = r.ReadInt32(),
                HostNickname  = r.ReadPktString(),
                HostReady     = r.ReadBoolean(),
                GuestPlayerId = r.ReadInt32(),
                GuestNickname = r.ReadPktString(),
                GuestReady    = r.ReadBoolean(),
                HostWins      = r.ReadInt32(),
                GuestWins     = r.ReadInt32(),
            };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_ROOM_STATE, w =>
        {
            w.Write((byte)RoomState);
            w.Write(HostPlayerId);
            w.WritePktString(HostNickname);
            w.Write(HostReady);
            w.Write(GuestPlayerId);
            w.WritePktString(GuestNickname);
            w.Write(GuestReady);
            w.Write(HostWins);
            w.Write(GuestWins);
        });
    }

    public class S_ROUND_START
    {
        public int              RoundNumber;
        public int              HostWins;
        public int              GuestWins;
        public long             ServerStartTime; // Unix ms
        public PlayerInitData[] Players = Array.Empty<PlayerInitData>();

        public static S_ROUND_START Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            var p = new S_ROUND_START
            {
                RoundNumber     = r.ReadInt32(),
                HostWins        = r.ReadInt32(),
                GuestWins       = r.ReadInt32(),
                ServerStartTime = r.ReadInt64(),
            };
            ushort count = r.ReadUInt16();
            p.Players = new PlayerInitData[count];
            for (int i = 0; i < count; i++) p.Players[i] = PlayerInitData.Deserialize(r);
            return p;
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_ROUND_START, w =>
        {
            w.Write(RoundNumber);
            w.Write(HostWins);
            w.Write(GuestWins);
            w.Write(ServerStartTime);
            w.Write((ushort)Players.Length);
            foreach (var p in Players) p.Serialize(w);
        });
    }

    public class S_ROUND_OVER
    {
        public int RoundWinnerPlayerId;
        public int RoundNumber;
        public int HostWins;
        public int GuestWins;

        public static S_ROUND_OVER Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_ROUND_OVER
            {
                RoundWinnerPlayerId = r.ReadInt32(),
                RoundNumber         = r.ReadInt32(),
                HostWins            = r.ReadInt32(),
                GuestWins           = r.ReadInt32(),
            };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_ROUND_OVER, w =>
        {
            w.Write(RoundWinnerPlayerId);
            w.Write(RoundNumber);
            w.Write(HostWins);
            w.Write(GuestWins);
        });
    }

    public class S_MATCH_OVER
    {
        public int MatchWinnerPlayerId;
        public int HostWins;
        public int GuestWins;

        public static S_MATCH_OVER Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_MATCH_OVER
            {
                MatchWinnerPlayerId = r.ReadInt32(),
                HostWins            = r.ReadInt32(),
                GuestWins           = r.ReadInt32(),
            };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_MATCH_OVER, w =>
        {
            w.Write(MatchWinnerPlayerId);
            w.Write(HostWins);
            w.Write(GuestWins);
        });
    }
}
