// 패킷 종류 식별자 및 룸 상태 열거형
namespace Shared
{
    public enum PacketId : ushort
    {
        // 연결/로비 (1000번대)
        C_LOGIN_REQ       = 1001,
        S_LOGIN_RES       = 1002,
        C_ROOM_LIST_REQ   = 1003,
        S_ROOM_LIST_RES   = 1004,
        C_CREATE_ROOM_REQ = 1005,
        S_CREATE_ROOM_RES = 1006,
        C_JOIN_ROOM_REQ   = 1007,
        S_JOIN_ROOM_RES   = 1008,
        S_PLAYER_JOINED   = 1009,
        S_PLAYER_LEFT     = 1010,
        C_LEAVE_ROOM_REQ  = 1011,
        S_ROOM_CLOSED     = 1012,

        // 룸 내 흐름 (2000번대)
        C_READY_REQ   = 2001,
        S_ROOM_STATE  = 2002,
        S_ROUND_START = 2003,
        S_ROUND_OVER  = 2004,
        S_MATCH_OVER  = 2005,

        // 인게임 (3000번대)
        C_INPUT           = 3001,
        S_GAME_STATE_SYNC = 3002,
        S_PLAYER_SHOT     = 3003,
        S_PLAYER_HIT      = 3004,

        // 공용 (9000번대)
        S_ERROR         = 9001,
        C_HEARTBEAT     = 9101,
        S_HEARTBEAT_ACK = 9102,
    }

    public enum RoomState : byte
    {
        Waiting   = 0,
        InRoom    = 1,
        Playing   = 2,
        RoundOver = 3,
        MatchOver = 4,
    }
}
