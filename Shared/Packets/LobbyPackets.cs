// 로비 패킷: 로그인, 룸 목록, 방 생성/입장/퇴장
using System;
using System.IO;

namespace Shared.Packets
{
    public class C_LOGIN_REQ
    {
        public string Nickname = string.Empty;

        public static C_LOGIN_REQ Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new C_LOGIN_REQ { Nickname = r.ReadPktString() };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_LOGIN_REQ,
            w => w.WritePktString(Nickname));
    }

    public class S_LOGIN_RES
    {
        public bool Success;
        public int  PlayerId;
        public long ServerTime; // Unix ms

        public static S_LOGIN_RES Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_LOGIN_RES
            {
                Success    = r.ReadBoolean(),
                PlayerId   = r.ReadInt32(),
                ServerTime = r.ReadInt64(),
            };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_LOGIN_RES, w =>
        {
            w.Write(Success);
            w.Write(PlayerId);
            w.Write(ServerTime);
        });
    }

    public class C_ROOM_LIST_REQ
    {
        public static C_ROOM_LIST_REQ Deserialize(byte[] payload) => new C_ROOM_LIST_REQ();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_ROOM_LIST_REQ);
    }

    public class S_ROOM_LIST_RES
    {
        public RoomInfo[] Rooms = Array.Empty<RoomInfo>();

        public static S_ROOM_LIST_RES Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            ushort count = r.ReadUInt16();
            var rooms = new RoomInfo[count];
            for (int i = 0; i < count; i++) rooms[i] = RoomInfo.Deserialize(r);
            return new S_ROOM_LIST_RES { Rooms = rooms };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_ROOM_LIST_RES, w =>
        {
            w.Write((ushort)Rooms.Length);
            foreach (var room in Rooms) room.Serialize(w);
        });
    }

    public class C_CREATE_ROOM_REQ
    {
        public static C_CREATE_ROOM_REQ Deserialize(byte[] payload) => new C_CREATE_ROOM_REQ();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_CREATE_ROOM_REQ);
    }

    public class S_CREATE_ROOM_RES
    {
        public bool Success;
        public int  RoomId;

        public static S_CREATE_ROOM_RES Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_CREATE_ROOM_RES { Success = r.ReadBoolean(), RoomId = r.ReadInt32() };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_CREATE_ROOM_RES, w =>
        {
            w.Write(Success);
            w.Write(RoomId);
        });
    }

    public class C_JOIN_ROOM_REQ
    {
        public int RoomId;

        public static C_JOIN_ROOM_REQ Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new C_JOIN_ROOM_REQ { RoomId = r.ReadInt32() };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_JOIN_ROOM_REQ,
            w => w.Write(RoomId));
    }

    public class S_JOIN_ROOM_RES
    {
        public bool   Success;
        public string ErrorMessage = string.Empty;

        public static S_JOIN_ROOM_RES Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_JOIN_ROOM_RES { Success = r.ReadBoolean(), ErrorMessage = r.ReadPktString() };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_JOIN_ROOM_RES, w =>
        {
            w.Write(Success);
            w.WritePktString(ErrorMessage);
        });
    }

    // 방장에게 전송: 게스트가 입장했음
    public class S_PLAYER_JOINED
    {
        public int    GuestPlayerId;
        public string GuestNickname = string.Empty;

        public static S_PLAYER_JOINED Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_PLAYER_JOINED { GuestPlayerId = r.ReadInt32(), GuestNickname = r.ReadPktString() };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_PLAYER_JOINED, w =>
        {
            w.Write(GuestPlayerId);
            w.WritePktString(GuestNickname);
        });
    }

    // 방장에게 전송: 게스트가 나감
    public class S_PLAYER_LEFT
    {
        public static S_PLAYER_LEFT Deserialize(byte[] payload) => new S_PLAYER_LEFT();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_PLAYER_LEFT);
    }

    public class C_LEAVE_ROOM_REQ
    {
        public static C_LEAVE_ROOM_REQ Deserialize(byte[] payload) => new C_LEAVE_ROOM_REQ();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_LEAVE_ROOM_REQ);
    }

    // 게스트에게 전송: 방장이 나가 방이 사라짐
    public class S_ROOM_CLOSED
    {
        public static S_ROOM_CLOSED Deserialize(byte[] payload) => new S_ROOM_CLOSED();
        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_ROOM_CLOSED);
    }
}
