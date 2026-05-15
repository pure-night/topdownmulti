// 전체 클라이언트 세션 관리 (등록/제거/조회)
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Server.Network
{
    public class SessionManager
    {
        private readonly ConcurrentDictionary<int, ClientSession> _sessions = new();

        public void Register(ClientSession session)
        {
            _sessions[session.SessionId] = session;
            session.OnDisconnected += Remove;
        }

        public void Remove(ClientSession session)
        {
            _sessions.TryRemove(session.SessionId, out _);
        }

        public ClientSession? GetByPlayerId(int playerId)
        {
            foreach (var s in _sessions.Values)
                if (s.PlayerId == playerId) return s;
            return null;
        }

        public IEnumerable<ClientSession> GetAll() => _sessions.Values;

        public int Count => _sessions.Count;
    }
}
