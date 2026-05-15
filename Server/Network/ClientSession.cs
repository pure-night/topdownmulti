// 개별 클라이언트 세션: 수신 루프, 패킷 버퍼링, 송신
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Server.Handler;

namespace Server.Network
{
    public class ClientSession
    {
        private static int _nextSessionId = 0;

        public int      SessionId { get; } = Interlocked.Increment(ref _nextSessionId);
        public int      PlayerId  { get; set; } = -1;
        public string   Nickname  { get; set; } = string.Empty;
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

        private readonly TcpClient        _tcpClient;
        private readonly NetworkStream    _stream;
        private readonly PacketDispatcher _dispatcher;
        private readonly SemaphoreSlim    _sendLock = new SemaphoreSlim(1, 1);

        // 수신 버퍼: 최대 패킷 2개 분량 확보
        private readonly byte[] _recvBuffer = new byte[PacketConstants.MaxPacketSize * 2];
        private int _bufferLength = 0;

        public event Action<ClientSession>? OnDisconnected;

        public ClientSession(TcpClient tcpClient, PacketDispatcher dispatcher)
        {
            _tcpClient  = tcpClient;
            _stream     = tcpClient.GetStream();
            _dispatcher = dispatcher;

            // Nagle 알고리즘 비활성화 → 소량 패킷도 즉시 전송
            tcpClient.NoDelay = true;
        }

        public void Start() => _ = Task.Run(ReceiveLoopAsync);

        private async Task ReceiveLoopAsync()
        {
            Console.WriteLine($"[Session {SessionId}] Connected: {_tcpClient.Client.RemoteEndPoint}");
            try
            {
                while (true)
                {
                    // 버퍼가 꽉 찼으면 비정상 클라이언트 → 연결 종료
                    if (_bufferLength >= _recvBuffer.Length)
                    {
                        Console.WriteLine($"[Session {SessionId}] Receive buffer full → disconnect");
                        break;
                    }

                    int bytesRead = await _stream.ReadAsync(
                        _recvBuffer, _bufferLength, _recvBuffer.Length - _bufferLength);

                    if (bytesRead == 0) break; // 연결 종료

                    _bufferLength += bytesRead;

                    // 버퍼에서 완성 패킷을 최대한 파싱
                    int consumed = 0;
                    while (PacketSerializer.TryParse(
                        _recvBuffer, consumed, _bufferLength - consumed,
                        out int packetLength, out PacketId packetId, out byte[] payload))
                    {
                        LastHeartbeat = DateTime.UtcNow;
                        _dispatcher.Dispatch(this, packetId, payload);
                        consumed += packetLength;
                    }

                    // 처리된 바이트 제거 (앞으로 당기기)
                    if (consumed > 0)
                    {
                        _bufferLength -= consumed;
                        if (_bufferLength > 0)
                            Array.Copy(_recvBuffer, consumed, _recvBuffer, 0, _bufferLength);
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine($"[Session {SessionId}] Protocol error: {ex.Message} → disconnect");
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // 정상 종료 또는 네트워크 단절
            }
            finally
            {
                Disconnect();
            }
        }

        // 비동기 송신 (동시 쓰기 방지를 위해 SemaphoreSlim 사용)
        public async Task SendAsync(byte[] packet)
        {
            if (!_tcpClient.Connected) return;
            await _sendLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {SessionId}] Send error: {ex.Message}");
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // Fire-and-forget 송신 (게임 루프 등 동기 컨텍스트에서 사용)
        public void Send(byte[] packet) => _ = SendAsync(packet);

        public void Disconnect()
        {
            try { _tcpClient.Close(); } catch { }
            Console.WriteLine($"[Session {SessionId}] Disconnected (PlayerId={PlayerId}, Nick={Nickname})");
            OnDisconnected?.Invoke(this);
        }
    }
}
