// 패킷 ID → 핸들러 Action 매핑 및 디스패치
using System;
using System.Collections.Generic;
using Shared;
using Server.Network;

namespace Server.Handler
{
    public class PacketDispatcher
    {
        private readonly Dictionary<PacketId, Action<ClientSession, byte[]>> _handlers = new();

        public void Register(PacketId id, Action<ClientSession, byte[]> handler)
        {
            _handlers[id] = handler;
        }

        public void Dispatch(ClientSession session, PacketId id, byte[] payload)
        {
            if (_handlers.TryGetValue(id, out var handler))
                handler(session, payload);
            else
                Console.WriteLine($"[Dispatcher] Unhandled packet {id} from session {session.SessionId}");
        }
    }
}
