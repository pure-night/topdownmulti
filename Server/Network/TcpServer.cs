// TCP 리스너: 클라이언트 연결 수락 및 세션 생성
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.Handler;

namespace Server.Network
{
    public class TcpServer
    {
        private readonly PacketDispatcher _dispatcher;
        private readonly SessionManager   _sessionManager;
        private TcpListener?              _listener;

        public TcpServer(PacketDispatcher dispatcher, SessionManager sessionManager)
        {
            _dispatcher     = dispatcher;
            _sessionManager = sessionManager;
        }

        public async Task StartAsync(string host, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(host), port);
            _listener.Start();
            Console.WriteLine($"[Server] Listening on {host}:{port}");

            while (true)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                var session = new ClientSession(tcpClient, _dispatcher);
                _sessionManager.Register(session);
                session.Start();
            }
        }

        public void Stop() => _listener?.Stop();
    }
}
