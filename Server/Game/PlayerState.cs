// 서버 측 플레이어 상태 (위치, HP, 입력, 쿨타임)
namespace Server.Game
{
    public class PlayerState
    {
        public int    PlayerId;
        public string Nickname = string.Empty;

        // 게임 상태
        public float PosX;
        public float PosY;
        public int   Hp;
        public float AimDirX;
        public float AimDirY;
        public float ShootCooldown; // 남은 쿨타임 (초)

        // 룸 상태
        public bool IsReady;

        // 최신 클라이언트 입력 (수신 스레드에서 덮어씀)
        public float MoveDirX;
        public float MoveDirY;
        public bool  ShootPressed;
        public uint  LastInputSeq;
    }
}
