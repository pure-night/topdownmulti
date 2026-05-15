// 인게임 패킷 핸들러: C_INPUT 수신 → 해당 룸의 입력 큐에 전달
using Shared;
using Shared.Packets;
using Server.Network;
using Server.Room;

namespace Server.Handler
{
    public static class InGameHandler
    {
        public static void Register(PacketDispatcher dispatcher, RoomManager roomManager)
        {
            dispatcher.Register(PacketId.C_INPUT, (session, payload) =>
            {
                if (session.PlayerId < 0) return;
                var input = C_INPUT.Deserialize(payload);
                roomManager.GetRoomByPlayerId(session.PlayerId)?.EnqueueInput(session.PlayerId, input);
            });
        }
    }
}
