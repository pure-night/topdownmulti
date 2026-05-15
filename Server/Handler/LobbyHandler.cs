// 로비 패킷 핸들러: 로그인, 룸 목록/생성/입장/퇴장/Ready, 하트비트
using System;
using System.Linq;
using System.Threading;
using Shared;
using Shared.Packets;
using Server.Network;
using Server.Room;

namespace Server.Handler
{
    public static class LobbyHandler
    {
        private static int _nextPlayerId = 0;

        public static void Register(PacketDispatcher dispatcher, SessionManager sessionManager, RoomManager roomManager)
        {
            dispatcher.Register(PacketId.C_LOGIN_REQ,      (s, p) => HandleLogin(s, p));
            dispatcher.Register(PacketId.C_ROOM_LIST_REQ,  (s, p) => HandleRoomList(s, roomManager));
            dispatcher.Register(PacketId.C_CREATE_ROOM_REQ,(s, p) => HandleCreateRoom(s, roomManager));
            dispatcher.Register(PacketId.C_JOIN_ROOM_REQ,  (s, p) => HandleJoinRoom(s, p, roomManager));
            dispatcher.Register(PacketId.C_LEAVE_ROOM_REQ, (s, p) => HandleLeaveRoom(s, roomManager));
            dispatcher.Register(PacketId.C_READY_REQ,      (s, p) => HandleReady(s, roomManager));
            dispatcher.Register(PacketId.C_HEARTBEAT,      (s, p) => HandleHeartbeat(s));
        }

        private static void HandleLogin(ClientSession session, byte[] payload)
        {
            var req = C_LOGIN_REQ.Deserialize(payload);
            if (string.IsNullOrWhiteSpace(req.Nickname))
            {
                session.Send(new S_ERROR { Message = "Invalid nickname" }.Serialize());
                return;
            }

            int playerId = Interlocked.Increment(ref _nextPlayerId);
            session.PlayerId = playerId;
            session.Nickname = req.Nickname;

            session.Send(new S_LOGIN_RES
            {
                Success    = true,
                PlayerId   = playerId,
                ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            }.Serialize());

            Console.WriteLine($"[Login] Session {session.SessionId} → PlayerId={playerId}, Nick={req.Nickname}");
        }

        private static void HandleRoomList(ClientSession session, RoomManager roomManager)
        {
            var rooms = roomManager.GetAll()
                .Where(r => r.Phase == RoomState.Waiting)
                .Select(r => new RoomInfo { RoomId = r.RoomId, HostNickname = r.HostNickname, PlayerCount = 1 })
                .ToArray();

            session.Send(new S_ROOM_LIST_RES { Rooms = rooms }.Serialize());
        }

        private static void HandleCreateRoom(ClientSession session, RoomManager roomManager)
        {
            if (session.PlayerId < 0)
            {
                session.Send(new S_ERROR { Message = "Not logged in" }.Serialize());
                return;
            }
            if (roomManager.GetRoomByPlayerId(session.PlayerId) != null)
            {
                session.Send(new S_ERROR { Message = "Already in a room" }.Serialize());
                return;
            }

            var room = roomManager.CreateRoom(session);
            session.Send(new S_CREATE_ROOM_RES { Success = true, RoomId = room.RoomId }.Serialize());
            Console.WriteLine($"[Room] Created Room {room.RoomId} by {session.Nickname}");
        }

        private static void HandleJoinRoom(ClientSession session, byte[] payload, RoomManager roomManager)
        {
            if (session.PlayerId < 0)
            {
                session.Send(new S_ERROR { Message = "Not logged in" }.Serialize());
                return;
            }
            if (roomManager.GetRoomByPlayerId(session.PlayerId) != null)
            {
                session.Send(new S_JOIN_ROOM_RES { Success = false, ErrorMessage = "Already in a room" }.Serialize());
                return;
            }

            var req  = C_JOIN_ROOM_REQ.Deserialize(payload);
            var room = roomManager.GetRoom(req.RoomId);

            if (room == null)
            {
                session.Send(new S_JOIN_ROOM_RES { Success = false, ErrorMessage = "Room not found" }.Serialize());
                return;
            }

            if (!room.TryJoin(session))
            {
                session.Send(new S_JOIN_ROOM_RES { Success = false, ErrorMessage = "Room is full" }.Serialize());
                return;
            }

            session.Send(new S_JOIN_ROOM_RES { Success = true }.Serialize());
            Console.WriteLine($"[Room] {session.Nickname} joined Room {room.RoomId}");
        }

        private static void HandleLeaveRoom(ClientSession session, RoomManager roomManager)
        {
            var room = roomManager.GetRoomByPlayerId(session.PlayerId);
            room?.OnPlayerLeave(session);
        }

        private static void HandleReady(ClientSession session, RoomManager roomManager)
        {
            var room = roomManager.GetRoomByPlayerId(session.PlayerId);
            room?.OnReady(session);
        }

        private static void HandleHeartbeat(ClientSession session)
        {
            session.LastHeartbeat = DateTime.UtcNow;
            session.Send(new S_HEARTBEAT_ACK().Serialize());
        }
    }
}
