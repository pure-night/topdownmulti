// 서버 통합 테스트 클라이언트 — 로그인/룸/게임 전체 흐름 검증
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Packets;

const string Host = "127.0.0.1";
const int    Port = 7777;

// ── 공유 상태 ─────────────────────────────────────────────────
var sendLock = new SemaphoreSlim(1, 1);
var stream   = default(System.Net.Sockets.NetworkStream)!;

var phase      = Phase.Lobby;
int myPlayerId = -1;
int myRoomId   = -1;

// 게임 루프 입력 상태 (메인 스레드만 씀, 루프 태스크만 읽음)
float moveDirX  = 0f, moveDirY  = 0f;
float aimDirX   = 1f, aimDirY   = 0f; // 기본 조준: 오른쪽
bool  shootOn   = false;
uint  inputSeq  = 0;
bool  verboseSync = false; // V키로 토글: S_GAME_STATE_SYNC 출력 여부

// ── 연결 ─────────────────────────────────────────────────────
Console.WriteLine($"Connecting to {Host}:{Port}...");
var client = new TcpClient();
await client.ConnectAsync(Host, Port);
client.NoDelay = true;
stream = client.GetStream();
Console.WriteLine("Connected.\n");

// ── 수신 루프 ─────────────────────────────────────────────────
var recvBuf = new byte[PacketConstants.MaxPacketSize * 2];
int bufLen  = 0;

_ = Task.Run(async () =>
{
    try
    {
        while (true)
        {
            int read = await stream.ReadAsync(recvBuf, bufLen, recvBuf.Length - bufLen);
            if (read == 0) { Console.WriteLine("\n[서버 연결 종료]"); break; }
            bufLen += read;

            int consumed = 0;
            while (PacketSerializer.TryParse(recvBuf, consumed, bufLen - consumed,
                       out int len, out PacketId id, out byte[] payload))
            {
                OnPacket(id, payload);
                consumed += len;
            }
            if (consumed > 0)
            {
                bufLen -= consumed;
                if (bufLen > 0) Array.Copy(recvBuf, consumed, recvBuf, 0, bufLen);
            }
        }
    }
    catch (Exception ex) { Console.WriteLine($"[Recv error] {ex.Message}"); }
});

// ── 게임 입력 루프 (Playing 상태에서 50ms마다 C_INPUT 전송) ────
_ = Task.Run(async () =>
{
    while (true)
    {
        if (phase == Phase.Playing)
        {
            await SendAsync(new C_INPUT
            {
                InputSeq     = ++inputSeq,
                MoveDirX     = moveDirX,
                MoveDirY     = moveDirY,
                AimDirX      = aimDirX,
                AimDirY      = aimDirY,
                ShootPressed = shootOn,
            }.Serialize());
        }
        await Task.Delay(50);
    }
});

// ── 로그인 ────────────────────────────────────────────────────
Console.Write("닉네임 입력: ");
var nick = Console.ReadLine() ?? "TestPlayer";
await SendAsync(new C_LOGIN_REQ { Nickname = nick }.Serialize());
Console.WriteLine($"[Send] C_LOGIN_REQ Nick={nick}");

PrintHelp();

// ── 키 입력 루프 (메인 스레드) ───────────────────────────────
while (true)
{
    var key = Console.ReadKey(true);

    // ── 공통 ──
    if (key.Key == ConsoleKey.Q) break;
    if (key.Key == ConsoleKey.H)
    {
        await SendAsync(new C_HEARTBEAT().Serialize());
        Console.WriteLine("[Send] C_HEARTBEAT");
        continue;
    }
    if (key.Key == ConsoleKey.F1)
    {
        PrintHelp(); continue;
    }

    // ── 로비 ──
    if (phase == Phase.Lobby)
    {
        if (key.Key == ConsoleKey.L)
        {
            await SendAsync(new C_ROOM_LIST_REQ().Serialize());
            Console.WriteLine("[Send] C_ROOM_LIST_REQ");
        }
        else if (key.Key == ConsoleKey.C)
        {
            await SendAsync(new C_CREATE_ROOM_REQ().Serialize());
            Console.WriteLine("[Send] C_CREATE_ROOM_REQ");
        }
        else if (key.Key == ConsoleKey.J)
        {
            Console.Write("입장할 Room ID: ");
            if (int.TryParse(Console.ReadLine(), out int rid))
            {
                await SendAsync(new C_JOIN_ROOM_REQ { RoomId = rid }.Serialize());
                Console.WriteLine($"[Send] C_JOIN_ROOM_REQ RoomId={rid}");
            }
        }
        continue;
    }

    // ── 방 대기 (InRoom) ──
    if (phase == Phase.InRoom)
    {
        if (key.Key == ConsoleKey.R)
        {
            await SendAsync(new C_READY_REQ().Serialize());
            Console.WriteLine("[Send] C_READY_REQ");
        }
        else if (key.Key == ConsoleKey.X)
        {
            await SendAsync(new C_LEAVE_ROOM_REQ().Serialize());
            Console.WriteLine("[Send] C_LEAVE_ROOM_REQ");
        }
        continue;
    }

    // ── 인게임 (Playing) ──
    if (phase == Phase.Playing)
    {
        // 이동: WASD
        if      (key.Key == ConsoleKey.W) { moveDirX =  0; moveDirY =  1; Console.WriteLine("[Move] ↑"); }
        else if (key.Key == ConsoleKey.S) { moveDirX =  0; moveDirY = -1; Console.WriteLine("[Move] ↓"); }
        else if (key.Key == ConsoleKey.A) { moveDirX = -1; moveDirY =  0; Console.WriteLine("[Move] ←"); }
        else if (key.Key == ConsoleKey.D) { moveDirX =  1; moveDirY =  0; Console.WriteLine("[Move] →"); }
        else if (key.Key == ConsoleKey.Spacebar && key.Modifiers == 0)
        {
            moveDirX = moveDirY = 0;
            Console.WriteLine("[Move] 정지");
        }

        // 조준: 화살표
        else if (key.Key == ConsoleKey.UpArrow)    { aimDirX =  0; aimDirY =  1; Console.WriteLine("[Aim] ↑"); }
        else if (key.Key == ConsoleKey.DownArrow)  { aimDirX =  0; aimDirY = -1; Console.WriteLine("[Aim] ↓"); }
        else if (key.Key == ConsoleKey.LeftArrow)  { aimDirX = -1; aimDirY =  0; Console.WriteLine("[Aim] ←"); }
        else if (key.Key == ConsoleKey.RightArrow) { aimDirX =  1; aimDirY =  0; Console.WriteLine("[Aim] →"); }

        // 발사 토글: F
        else if (key.Key == ConsoleKey.F)
        {
            shootOn = !shootOn;
            Console.WriteLine($"[Shoot] {(shootOn ? "ON" : "OFF")}");
        }

        // SYNC 출력 토글: V
        else if (key.Key == ConsoleKey.V)
        {
            verboseSync = !verboseSync;
            Console.WriteLine($"[Verbose SYNC] {(verboseSync ? "ON" : "OFF")}");
        }

        // 게임 중 퇴장: X
        else if (key.Key == ConsoleKey.X)
        {
            await SendAsync(new C_LEAVE_ROOM_REQ().Serialize());
            Console.WriteLine("[Send] C_LEAVE_ROOM_REQ");
        }
    }
}

Console.WriteLine("종료.");

// ── 패킷 수신 핸들러 ─────────────────────────────────────────
void OnPacket(PacketId id, byte[] payload)
{
    switch (id)
    {
        case PacketId.S_LOGIN_RES:
        {
            var p = S_LOGIN_RES.Deserialize(payload);
            myPlayerId = p.PlayerId;
            Console.WriteLine($"\n[Recv] S_LOGIN_RES  Success={p.Success}  PlayerId={p.PlayerId}  ServerTime={p.ServerTime}");
            break;
        }
        case PacketId.S_ROOM_LIST_RES:
        {
            var p = S_ROOM_LIST_RES.Deserialize(payload);
            Console.WriteLine($"\n[Recv] S_ROOM_LIST_RES  방 {p.Rooms.Length}개");
            foreach (var r in p.Rooms)
                Console.WriteLine($"         RoomId={r.RoomId}  Host={r.HostNickname}  Players={r.PlayerCount}");
            break;
        }
        case PacketId.S_CREATE_ROOM_RES:
        {
            var p = S_CREATE_ROOM_RES.Deserialize(payload);
            myRoomId = p.RoomId;
            Console.WriteLine($"\n[Recv] S_CREATE_ROOM_RES  Success={p.Success}  RoomId={p.RoomId}");
            break;
        }
        case PacketId.S_JOIN_ROOM_RES:
        {
            var p = S_JOIN_ROOM_RES.Deserialize(payload);
            Console.WriteLine($"\n[Recv] S_JOIN_ROOM_RES  Success={p.Success}  {(p.Success ? "" : $"Error={p.ErrorMessage}")}");
            break;
        }
        case PacketId.S_PLAYER_JOINED:
        {
            var p = S_PLAYER_JOINED.Deserialize(payload);
            Console.WriteLine($"\n[Recv] S_PLAYER_JOINED  GuestId={p.GuestPlayerId}  Nick={p.GuestNickname}");
            break;
        }
        case PacketId.S_PLAYER_LEFT:
            Console.WriteLine("\n[Recv] S_PLAYER_LEFT  (상대방이 나갔습니다)");
            phase = Phase.Lobby;
            PrintHelp();
            break;
        case PacketId.S_ROOM_CLOSED:
            Console.WriteLine("\n[Recv] S_ROOM_CLOSED  (방장이 나가 방이 사라졌습니다)");
            phase = Phase.Lobby;
            PrintHelp();
            break;
        case PacketId.S_ROOM_STATE:
        {
            var p = S_ROOM_STATE.Deserialize(payload);
            phase = p.RoomState is RoomState.Waiting or RoomState.InRoom ? Phase.InRoom : phase;
            if (p.RoomState == RoomState.InRoom) phase = Phase.InRoom;
            Console.WriteLine($"\n[Recv] S_ROOM_STATE  State={p.RoomState}");
            Console.WriteLine($"         Host={p.HostNickname}({p.HostPlayerId}) Ready={p.HostReady}");
            Console.WriteLine($"         Guest={p.GuestNickname}({p.GuestPlayerId}) Ready={p.GuestReady}");
            Console.WriteLine($"         Score={p.HostWins}:{p.GuestWins}");
            break;
        }
        case PacketId.S_ROUND_START:
        {
            var p = S_ROUND_START.Deserialize(payload);
            phase = Phase.Playing;
            shootOn = false; inputSeq = 0;
            moveDirX = moveDirY = 0; aimDirX = 1; aimDirY = 0;
            Console.WriteLine($"\n[Recv] S_ROUND_START  Round={p.RoundNumber}  Score={p.HostWins}:{p.GuestWins}");
            foreach (var pl in p.Players)
                Console.WriteLine($"         PlayerId={pl.PlayerId}  Nick={pl.Nickname}  Pos=({pl.StartPosX},{pl.StartPosY})");
            Console.WriteLine("  → 게임 시작! WASD=이동  화살표=조준  F=발사토글  V=SYNC출력  X=퇴장");
            break;
        }
        case PacketId.S_ROUND_OVER:
        {
            var p = S_ROUND_OVER.Deserialize(payload);
            phase = Phase.InRoom;
            shootOn = false;
            Console.WriteLine($"\n[Recv] S_ROUND_OVER  Round={p.RoundNumber}  Winner={p.RoundWinnerPlayerId}  Score={p.HostWins}:{p.GuestWins}");
            break;
        }
        case PacketId.S_MATCH_OVER:
        {
            var p = S_MATCH_OVER.Deserialize(payload);
            Console.WriteLine($"\n[Recv] S_MATCH_OVER  Winner={p.MatchWinnerPlayerId}  Final={p.HostWins}:{p.GuestWins}");
            break;
        }
        case PacketId.S_PLAYER_SHOT:
        {
            var p = S_PLAYER_SHOT.Deserialize(payload);
            Console.WriteLine($"[Recv] S_PLAYER_SHOT  Shooter={p.ShooterId}  Origin=({p.OriginX:F1},{p.OriginY:F1})  Dir=({p.DirX:F1},{p.DirY:F1})");
            break;
        }
        case PacketId.S_PLAYER_HIT:
        {
            var p = S_PLAYER_HIT.Deserialize(payload);
            bool isMe = p.TargetPlayerId == myPlayerId;
            Console.WriteLine($"[Recv] S_PLAYER_HIT  Target={p.TargetPlayerId}{(isMe?" ← 나":"")}  Dmg={p.Damage}  HP={p.RemainingHp}");
            break;
        }
        case PacketId.S_GAME_STATE_SYNC:
        {
            if (!verboseSync) break;
            var p = S_GAME_STATE_SYNC.Deserialize(payload);
            Console.Write($"[SYNC] t={p.ServerTime % 100000}  ");
            foreach (var pl in p.Players)
                Console.Write($"P{pl.PlayerId}({pl.PosX:F1},{pl.PosY:F1})HP={pl.Hp}  ");
            Console.WriteLine($"Bullets={p.Bullets.Length}");
            break;
        }
        case PacketId.S_HEARTBEAT_ACK:
            Console.WriteLine("[Recv] S_HEARTBEAT_ACK");
            break;
        case PacketId.S_ERROR:
        {
            var p = S_ERROR.Deserialize(payload);
            Console.WriteLine($"\n[Recv] S_ERROR  {p.Message}");
            break;
        }
        default:
            Console.WriteLine($"[Recv] {id} ({payload.Length}B)");
            break;
    }
}

// ── 전송 헬퍼 ────────────────────────────────────────────────
async Task SendAsync(byte[] packet)
{
    await sendLock.WaitAsync();
    try   { await stream.WriteAsync(packet); }
    finally { sendLock.Release(); }
}

void PrintHelp()
{
    Console.WriteLine();
    if (phase == Phase.Lobby)
    {
        Console.WriteLine("=== 로비 ===");
        Console.WriteLine("  [C] 방 만들기   [L] 방 목록   [J] 방 입장");
        Console.WriteLine("  [H] 하트비트    [Q] 종료");
    }
    else if (phase == Phase.InRoom)
    {
        Console.WriteLine("=== 방 대기 ===");
        Console.WriteLine("  [R] Ready 토글  [X] 방 나가기");
        Console.WriteLine("  [H] 하트비트    [Q] 종료");
    }
    else
    {
        Console.WriteLine("=== 인게임 ===");
        Console.WriteLine("  WASD=이동방향  화살표=조준방향  [F]=발사토글  스페이스=정지");
        Console.WriteLine("  [V]=SYNC출력토글  [X]=퇴장  [Q]=종료");
    }
}

// 타입 선언은 top-level statements 뒤에 위치해야 함
enum Phase { Lobby, InRoom, Playing }
