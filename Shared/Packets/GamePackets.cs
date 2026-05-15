// 인게임 패킷: 입력, 전체 상태 동기화, 발사/피격 이펙트
using System;
using System.IO;

namespace Shared.Packets
{
    // 매 틱(50ms) 클라이언트 → 서버: 통합 입력
    public class C_INPUT
    {
        public uint  InputSeq;
        public float MoveDirX;
        public float MoveDirY;
        public float AimDirX;
        public float AimDirY;
        public bool  ShootPressed;

        public static C_INPUT Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new C_INPUT
            {
                InputSeq     = r.ReadUInt32(),
                MoveDirX     = r.ReadSingle(),
                MoveDirY     = r.ReadSingle(),
                AimDirX      = r.ReadSingle(),
                AimDirY      = r.ReadSingle(),
                ShootPressed = r.ReadBoolean(),
            };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.C_INPUT, w =>
        {
            w.Write(InputSeq);
            w.Write(MoveDirX);
            w.Write(MoveDirY);
            w.Write(AimDirX);
            w.Write(AimDirY);
            w.Write(ShootPressed);
        });
    }

    // 매 틱(50ms) 서버 → 양쪽: 전체 게임 상태 스냅샷
    public class S_GAME_STATE_SYNC
    {
        public long             ServerTime;
        public uint             LastProcessedInputSeq;
        public PlayerStateData[] Players = Array.Empty<PlayerStateData>();
        public BulletStateData[] Bullets = Array.Empty<BulletStateData>();

        public static S_GAME_STATE_SYNC Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            var p = new S_GAME_STATE_SYNC
            {
                ServerTime            = r.ReadInt64(),
                LastProcessedInputSeq = r.ReadUInt32(),
            };
            ushort pc = r.ReadUInt16();
            p.Players = new PlayerStateData[pc];
            for (int i = 0; i < pc; i++) p.Players[i] = PlayerStateData.Deserialize(r);
            ushort bc = r.ReadUInt16();
            p.Bullets = new BulletStateData[bc];
            for (int i = 0; i < bc; i++) p.Bullets[i] = BulletStateData.Deserialize(r);
            return p;
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_GAME_STATE_SYNC, w =>
        {
            w.Write(ServerTime);
            w.Write(LastProcessedInputSeq);
            w.Write((ushort)Players.Length);
            foreach (var p in Players) p.Serialize(w);
            w.Write((ushort)Bullets.Length);
            foreach (var b in Bullets) b.Serialize(w);
        });
    }

    // 발사 이펙트용 (즉각적인 시각 피드백)
    public class S_PLAYER_SHOT
    {
        public int   ShooterId;
        public float OriginX;
        public float OriginY;
        public float DirX;
        public float DirY;

        public static S_PLAYER_SHOT Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_PLAYER_SHOT
            {
                ShooterId = r.ReadInt32(),
                OriginX   = r.ReadSingle(),
                OriginY   = r.ReadSingle(),
                DirX      = r.ReadSingle(),
                DirY      = r.ReadSingle(),
            };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_PLAYER_SHOT, w =>
        {
            w.Write(ShooterId);
            w.Write(OriginX);
            w.Write(OriginY);
            w.Write(DirX);
            w.Write(DirY);
        });
    }

    // 피격 이펙트 및 HP 갱신용
    public class S_PLAYER_HIT
    {
        public int TargetPlayerId;
        public int AttackerPlayerId;
        public int Damage;
        public int RemainingHp;

        public static S_PLAYER_HIT Deserialize(byte[] payload)
        {
            using var r = new BinaryReader(new MemoryStream(payload));
            return new S_PLAYER_HIT
            {
                TargetPlayerId   = r.ReadInt32(),
                AttackerPlayerId = r.ReadInt32(),
                Damage           = r.ReadInt32(),
                RemainingHp      = r.ReadInt32(),
            };
        }

        public byte[] Serialize() => PacketSerializer.Build(PacketId.S_PLAYER_HIT, w =>
        {
            w.Write(TargetPlayerId);
            w.Write(AttackerPlayerId);
            w.Write(Damage);
            w.Write(RemainingHp);
        });
    }
}
