// 룸 생성/조회/플레이어-룸 매핑 관리
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Server.Network;

namespace Server.Room
{
    public class RoomManager
    {
        private static int _nextRoomId = 0;

        private readonly ConcurrentDictionary<int, GameRoom> _rooms       = new();
        private readonly ConcurrentDictionary<int, GameRoom> _playerRooms = new(); // playerId → room

        public GameRoom CreateRoom(ClientSession hostSession)
        {
            int id   = Interlocked.Increment(ref _nextRoomId);
            var room = new GameRoom(id, hostSession, this);
            _rooms[id] = room;
            _playerRooms[hostSession.PlayerId] = room;
            return room;
        }

        public GameRoom? GetRoom(int roomId)
        {
            _rooms.TryGetValue(roomId, out var room);
            return room;
        }

        public GameRoom? GetRoomByPlayerId(int playerId)
        {
            _playerRooms.TryGetValue(playerId, out var room);
            return room;
        }

        public IEnumerable<GameRoom> GetAll() => _rooms.Values;

        // GameRoom이 호출 — 플레이어가 룸에 들어올 때
        internal void OnPlayerJoined(int playerId, GameRoom room)
        {
            _playerRooms[playerId] = room;
        }

        // GameRoom이 호출 — 플레이어가 룸에서 나갈 때
        internal void OnPlayerLeft(int playerId)
        {
            _playerRooms.TryRemove(playerId, out _);
        }

        // GameRoom이 호출 — 룸 파괴
        internal void OnRoomDestroyed(int roomId)
        {
            _rooms.TryRemove(roomId, out _);
        }
    }
}
