// 패킷 페이로드에 공통으로 사용되는 복합 데이터 타입
using System.IO;

namespace Shared.Packets
{
    public struct RoomInfo
    {
        public int    RoomId;
        public string HostNickname;
        public int    PlayerCount;

        public static RoomInfo Deserialize(BinaryReader r) => new RoomInfo
        {
            RoomId        = r.ReadInt32(),
            HostNickname  = r.ReadPktString(),
            PlayerCount   = r.ReadByte(),
        };

        public void Serialize(BinaryWriter w)
        {
            w.Write(RoomId);
            w.WritePktString(HostNickname);
            w.Write((byte)PlayerCount);
        }
    }

    public struct PlayerInitData
    {
        public int    PlayerId;
        public string Nickname;
        public float  StartPosX;
        public float  StartPosY;

        public static PlayerInitData Deserialize(BinaryReader r) => new PlayerInitData
        {
            PlayerId  = r.ReadInt32(),
            Nickname  = r.ReadPktString(),
            StartPosX = r.ReadSingle(),
            StartPosY = r.ReadSingle(),
        };

        public void Serialize(BinaryWriter w)
        {
            w.Write(PlayerId);
            w.WritePktString(Nickname);
            w.Write(StartPosX);
            w.Write(StartPosY);
        }
    }

    public struct PlayerStateData
    {
        public int   PlayerId;
        public float PosX;
        public float PosY;
        public int   Hp;
        public float AimDirX;
        public float AimDirY;

        public static PlayerStateData Deserialize(BinaryReader r) => new PlayerStateData
        {
            PlayerId = r.ReadInt32(),
            PosX     = r.ReadSingle(),
            PosY     = r.ReadSingle(),
            Hp       = r.ReadInt32(),
            AimDirX  = r.ReadSingle(),
            AimDirY  = r.ReadSingle(),
        };

        public void Serialize(BinaryWriter w)
        {
            w.Write(PlayerId);
            w.Write(PosX);
            w.Write(PosY);
            w.Write(Hp);
            w.Write(AimDirX);
            w.Write(AimDirY);
        }
    }

    public struct BulletStateData
    {
        public int   BulletId;
        public float PosX;
        public float PosY;
        public float DirX;
        public float DirY;

        public static BulletStateData Deserialize(BinaryReader r) => new BulletStateData
        {
            BulletId = r.ReadInt32(),
            PosX     = r.ReadSingle(),
            PosY     = r.ReadSingle(),
            DirX     = r.ReadSingle(),
            DirY     = r.ReadSingle(),
        };

        public void Serialize(BinaryWriter w)
        {
            w.Write(BulletId);
            w.Write(PosX);
            w.Write(PosY);
            w.Write(DirX);
            w.Write(DirY);
        }
    }
}
