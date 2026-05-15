// 서버 테스트용 더미 클라이언트
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Shared;
using Shared.Packets;

const string Host = "127.0.0.1";
const int    Port = 7777;

Console.WriteLine($"Connecting to {Host}:{Port}...");

using var client = new TcpClient();
await client.ConnectAsync(Host, Port);
client.NoDelay = true;

Console.WriteLine("Connected.");

var stream     = client.GetStream();
var recvBuffer = new byte[PacketConstants.MaxPacketSize * 2];
int bufferLen  = 0;

// 수신 루프 (별도 태스크)
var receiveTask = Task.Run(async () =>
{
    try
    {
        while (true)
        {
            int read = await stream.ReadAsync(recvBuffer, bufferLen, recvBuffer.Length - bufferLen);
            if (read == 0) { Console.WriteLine("[Disconnected]"); break; }
            bufferLen += read;

            int consumed = 0;
            while (PacketSerializer.TryParse(recvBuffer, consumed, bufferLen - consumed,
                       out int len, out PacketId id, out byte[] payload))
            {
                OnPacketReceived(id, payload);
                consumed += len;
            }
            if (consumed > 0)
            {
                bufferLen -= consumed;
                if (bufferLen > 0) Array.Copy(recvBuffer, consumed, recvBuffer, 0, bufferLen);
            }
        }
    }
    catch (Exception ex) { Console.WriteLine($"[Recv error] {ex.Message}"); }
});

// 로그인 요청
Console.Write("Enter nickname: ");
var nick = Console.ReadLine() ?? "TestPlayer";
var loginBytes = new C_LOGIN_REQ { Nickname = nick }.Serialize();
await stream.WriteAsync(loginBytes);
Console.WriteLine($"[Send] C_LOGIN_REQ Nickname={nick}");

// 명령 루프
Console.WriteLine("Commands: [H]eartbeat  [Q]uit");
while (true)
{
    var key = Console.ReadKey(true).Key;
    if (key == ConsoleKey.Q) break;
    if (key == ConsoleKey.H)
    {
        await stream.WriteAsync(new C_HEARTBEAT().Serialize());
        Console.WriteLine("[Send] C_HEARTBEAT");
    }
}

static void OnPacketReceived(PacketId id, byte[] payload)
{
    switch (id)
    {
        case PacketId.S_LOGIN_RES:
        {
            var p = S_LOGIN_RES.Deserialize(payload);
            Console.WriteLine($"[Recv] S_LOGIN_RES  Success={p.Success}  PlayerId={p.PlayerId}  ServerTime={p.ServerTime}");
            break;
        }
        case PacketId.S_ROOM_LIST_RES:
        {
            var p = S_ROOM_LIST_RES.Deserialize(payload);
            Console.WriteLine($"[Recv] S_ROOM_LIST_RES  Rooms={p.Rooms.Length}");
            break;
        }
        case PacketId.S_HEARTBEAT_ACK:
            Console.WriteLine("[Recv] S_HEARTBEAT_ACK");
            break;
        case PacketId.S_ERROR:
        {
            var p = S_ERROR.Deserialize(payload);
            Console.WriteLine($"[Recv] S_ERROR  Message={p.Message}");
            break;
        }
        default:
            Console.WriteLine($"[Recv] {id} ({payload.Length}B payload)");
            break;
    }
}
