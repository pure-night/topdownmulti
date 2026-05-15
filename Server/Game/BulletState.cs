// 서버 측 탄환 상태
namespace Server.Game
{
    public class BulletState
    {
        public int   BulletId;
        public int   OwnerId;
        public float PosX;
        public float PosY;
        public float DirX;
        public float DirY;
        public bool  IsAlive = true;
    }
}
