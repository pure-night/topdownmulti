// AABB 충돌 감지 유틸리티
using System;

namespace Server.Game
{
    public static class CollisionUtil
    {
        // 두 정사각형(중심 좌표, 한 변의 길이)이 겹치는지 검사
        public static bool Overlaps(
            float ax, float ay, float aSize,
            float bx, float by, float bSize)
        {
            float half = (aSize + bSize) * 0.5f;
            return Math.Abs(ax - bx) < half && Math.Abs(ay - by) < half;
        }
    }
}
