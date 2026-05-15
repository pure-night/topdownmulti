// 게임 룸: 상태 머신(Waiting→InRoom→Playing→RoundOver→MatchOver), 20Hz 게임 루프
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Packets;
using Server.Game;
using Server.Network;

namespace Server.Room
{
    public class GameRoom
    {
        public int       RoomId      { get; }
        public RoomState Phase       { get; private set; } = RoomState.Waiting;
        public string    HostNickname => _hostPlayer.Nickname;

        private readonly RoomManager   _roomManager;
        private readonly ClientSession _hostSession;
        private ClientSession?         _guestSession;

        private readonly PlayerState _hostPlayer;
        private PlayerState?         _guestPlayer;

        private readonly List<BulletState> _bullets      = new();
        private int _nextBulletId = 0;
        private int _hostWins     = 0;
        private int _guestWins    = 0;
        private int _roundNumber  = 0;

        private CancellationTokenSource? _loopCts;
        private readonly ConcurrentQueue<(int playerId, C_INPUT input)> _inputQueue = new();
        private bool _destroyed = false;

        // 게임 상수
        private const float MapMin       = 0f;
        private const float MapMax       = 20f;
        private const int   MaxHp        = 100;
        private const float PlayerSpeed  = 5f;
        private const float PlayerSize   = 1f;
        private const float BulletSpeed  = 15f;
        private const float BulletSize   = 0.3f;
        private const int   BulletDmg    = 20;
        private const float CooldownSec  = 0.3f;
        private const int   WinsNeeded   = 3;
        private const float TickSec      = 0.05f;  // 20Hz

        private static readonly (float x, float y)[] StartPositions =
        {
            (5f, 10f),   // Host (왼쪽)
            (15f, 10f),  // Guest (오른쪽)
        };

        public GameRoom(int roomId, ClientSession hostSession, RoomManager roomManager)
        {
            RoomId       = roomId;
            _roomManager = roomManager;
            _hostSession = hostSession;
            _hostPlayer  = new PlayerState
            {
                PlayerId = hostSession.PlayerId,
                Nickname = hostSession.Nickname,
            };

            hostSession.OnDisconnected += OnSessionDisconnected;
        }

        // ── 입장/퇴장 ─────────────────────────────────

        public bool TryJoin(ClientSession guestSession)
        {
            if (Phase != RoomState.Waiting || _guestSession != null) return false;

            _guestSession = guestSession;
            _guestPlayer  = new PlayerState
            {
                PlayerId = guestSession.PlayerId,
                Nickname = guestSession.Nickname,
            };

            Phase = RoomState.InRoom;
            guestSession.OnDisconnected += OnSessionDisconnected;
            _roomManager.OnPlayerJoined(guestSession.PlayerId, this);

            _hostSession.Send(new S_PLAYER_JOINED
            {
                GuestPlayerId = guestSession.PlayerId,
                GuestNickname = guestSession.Nickname,
            }.Serialize());

            BroadcastRoomState();
            Console.WriteLine($"[Room {RoomId}] Guest joined: {guestSession.Nickname}");
            return true;
        }

        public void OnPlayerLeave(ClientSession session)
        {
            if (_destroyed) return;
            if (session == _hostSession) HostLeft();
            else if (session == _guestSession) GuestLeft();
        }

        private void HostLeft()
        {
            Console.WriteLine($"[Room {RoomId}] Host left → room destroyed");
            _loopCts?.Cancel();
            _guestSession?.Send(new S_ROOM_CLOSED().Serialize());
            Destroy();
        }

        private void GuestLeft()
        {
            Console.WriteLine($"[Room {RoomId}] Guest left → back to Waiting");
            _loopCts?.Cancel();

            // 진행 중 라운드가 있으면 방장에게 라운드 승리
            if (Phase == RoomState.Playing)
            {
                _hostWins++;
                _hostSession.Send(new S_ROUND_OVER
                {
                    RoundWinnerPlayerId = _hostPlayer.PlayerId,
                    RoundNumber         = _roundNumber,
                    HostWins            = _hostWins,
                    GuestWins           = _guestWins,
                }.Serialize());
            }

            if (_guestPlayer != null)
                _roomManager.OnPlayerLeft(_guestPlayer.PlayerId);

            _guestSession = null;
            _guestPlayer  = null;
            Phase         = RoomState.Waiting;
            _hostWins     = 0;
            _guestWins    = 0;
            _hostPlayer.IsReady = false;

            _hostSession.Send(new S_PLAYER_LEFT().Serialize());
            BroadcastRoomState();
        }

        private void OnSessionDisconnected(ClientSession session) => OnPlayerLeave(session);

        // ── Ready ─────────────────────────────────────

        public void OnReady(ClientSession session)
        {
            if (Phase != RoomState.InRoom) return;

            if (session == _hostSession)
                _hostPlayer.IsReady = !_hostPlayer.IsReady;
            else if (session == _guestSession && _guestPlayer != null)
                _guestPlayer.IsReady = !_guestPlayer.IsReady;

            BroadcastRoomState();

            if (_hostPlayer.IsReady && (_guestPlayer?.IsReady ?? false))
                StartRound();
        }

        // ── 인게임 입력 (수신 스레드에서 호출) ──────────

        public void EnqueueInput(int playerId, C_INPUT input)
        {
            if (Phase == RoomState.Playing)
                _inputQueue.Enqueue((playerId, input));
        }

        // ── 라운드/매치 전환 ──────────────────────────

        private void StartRound()
        {
            if (_guestPlayer == null || _guestSession == null) return;

            _roundNumber++;
            Phase = RoomState.Playing;
            ResetRound();

            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            BroadcastToRoom(new S_ROUND_START
            {
                RoundNumber     = _roundNumber,
                HostWins        = _hostWins,
                GuestWins       = _guestWins,
                ServerStartTime = startTime,
                Players = new[]
                {
                    new PlayerInitData { PlayerId = _hostPlayer.PlayerId,  Nickname = _hostPlayer.Nickname,  StartPosX = _hostPlayer.PosX,  StartPosY = _hostPlayer.PosY  },
                    new PlayerInitData { PlayerId = _guestPlayer.PlayerId, Nickname = _guestPlayer.Nickname, StartPosX = _guestPlayer.PosX, StartPosY = _guestPlayer.PosY },
                },
            }.Serialize());

            Console.WriteLine($"[Room {RoomId}] Round {_roundNumber} start");

            _loopCts = new CancellationTokenSource();
            _ = Task.Run(() => GameLoopAsync(_loopCts.Token));
        }

        private void EndRound(int winnerPlayerId)
        {
            _loopCts?.Cancel();
            Phase = RoomState.RoundOver;

            if (winnerPlayerId == _hostPlayer.PlayerId) _hostWins++;
            else _guestWins++;

            Console.WriteLine($"[Room {RoomId}] Round {_roundNumber} over. Winner={winnerPlayerId}. Score={_hostWins}:{_guestWins}");

            BroadcastToRoom(new S_ROUND_OVER
            {
                RoundWinnerPlayerId = winnerPlayerId,
                RoundNumber         = _roundNumber,
                HostWins            = _hostWins,
                GuestWins           = _guestWins,
            }.Serialize());

            bool matchDecided = _hostWins >= WinsNeeded || _guestWins >= WinsNeeded;
            _ = Task.Delay(2000).ContinueWith(_ =>
            {
                if (_destroyed || Phase != RoomState.RoundOver) return;
                if (matchDecided) EndMatch();
                else StartRound();
            });
        }

        private void EndMatch()
        {
            Phase = RoomState.MatchOver;
            int matchWinner = _hostWins >= WinsNeeded ? _hostPlayer.PlayerId : _guestPlayer!.PlayerId;

            Console.WriteLine($"[Room {RoomId}] Match over. Winner={matchWinner}. Final={_hostWins}:{_guestWins}");

            BroadcastToRoom(new S_MATCH_OVER
            {
                MatchWinnerPlayerId = matchWinner,
                HostWins            = _hostWins,
                GuestWins           = _guestWins,
            }.Serialize());

            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                if (_destroyed || Phase != RoomState.MatchOver) return;
                _hostWins  = 0;
                _guestWins = 0;
                _hostPlayer.IsReady = false;
                if (_guestPlayer != null) _guestPlayer.IsReady = false;
                Phase = RoomState.InRoom;
                BroadcastRoomState();
                Console.WriteLine($"[Room {RoomId}] Back to InRoom");
            });
        }

        private void ResetRound()
        {
            _bullets.Clear();
            _nextBulletId = 0;

            ResetPlayer(_hostPlayer,  StartPositions[0]);
            if (_guestPlayer != null)
                ResetPlayer(_guestPlayer, StartPositions[1]);
        }

        private static void ResetPlayer(PlayerState p, (float x, float y) pos)
        {
            p.PosX = pos.x; p.PosY = pos.y;
            p.Hp   = MaxHp;
            p.ShootCooldown = 0;
            p.MoveDirX = p.MoveDirY = 0;
            p.AimDirX  = p.AimDirY  = 0;
            p.ShootPressed = false;
        }

        // ── 게임 루프 (20Hz) ──────────────────────────

        private async Task GameLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var start = DateTime.UtcNow;
                try { Tick(); }
                catch (Exception ex) { Console.WriteLine($"[Room {RoomId}] Tick error: {ex}"); }

                int delay = Math.Max(0, 50 - (int)(DateTime.UtcNow - start).TotalMilliseconds);
                try { await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void Tick()
        {
            if (_guestPlayer == null) return;

            // 1. 입력 큐 소비 (같은 플레이어의 복수 패킷 → 최신 것만 남김)
            while (_inputQueue.TryDequeue(out var item))
            {
                var (pid, inp) = item;
                var p = pid == _hostPlayer.PlayerId ? _hostPlayer : _guestPlayer;
                if (p == null) continue;

                (p.MoveDirX, p.MoveDirY) = ClampDir(inp.MoveDirX, inp.MoveDirY);
                (p.AimDirX,  p.AimDirY)  = NormalizeDir(inp.AimDirX, inp.AimDirY);
                p.ShootPressed = inp.ShootPressed;
                p.LastInputSeq = inp.InputSeq;
            }

            PlayerState[] players = { _hostPlayer, _guestPlayer };

            // 2. 쿨타임 감소
            foreach (var p in players)
                p.ShootCooldown = MathF.Max(0f, p.ShootCooldown - TickSec);

            // 3. 플레이어 이동 (맵 경계 클램프)
            float halfPlayer = PlayerSize * 0.5f;
            foreach (var p in players)
            {
                p.PosX = Math.Clamp(p.PosX + p.MoveDirX * PlayerSpeed * TickSec, MapMin + halfPlayer, MapMax - halfPlayer);
                p.PosY = Math.Clamp(p.PosY + p.MoveDirY * PlayerSpeed * TickSec, MapMin + halfPlayer, MapMax - halfPlayer);
            }

            // 4. 발사 처리
            foreach (var p in players)
            {
                if (!p.ShootPressed || p.ShootCooldown > 0f) continue;
                if (p.AimDirX == 0f && p.AimDirY == 0f) continue;

                var bullet = new BulletState
                {
                    BulletId = _nextBulletId++,
                    OwnerId  = p.PlayerId,
                    PosX = p.PosX, PosY = p.PosY,
                    DirX = p.AimDirX, DirY = p.AimDirY,
                };
                _bullets.Add(bullet);
                p.ShootCooldown = CooldownSec;

                BroadcastToRoom(new S_PLAYER_SHOT
                {
                    ShooterId = p.PlayerId,
                    OriginX = p.PosX, OriginY = p.PosY,
                    DirX = p.AimDirX, DirY = p.AimDirY,
                }.Serialize());
            }

            // 5. 탄환 이동 + 맵 밖 소멸
            foreach (var b in _bullets)
            {
                b.PosX += b.DirX * BulletSpeed * TickSec;
                b.PosY += b.DirY * BulletSpeed * TickSec;
                if (b.PosX < MapMin || b.PosX > MapMax || b.PosY < MapMin || b.PosY > MapMax)
                    b.IsAlive = false;
            }

            // 6. AABB 충돌 (탄환 → 상대 플레이어)
            int roundWinnerId = -1;
            foreach (var b in _bullets)
            {
                if (!b.IsAlive) continue;
                foreach (var p in players)
                {
                    if (p.PlayerId == b.OwnerId || p.Hp <= 0) continue;
                    if (!CollisionUtil.Overlaps(b.PosX, b.PosY, BulletSize, p.PosX, p.PosY, PlayerSize)) continue;

                    b.IsAlive = false;
                    p.Hp = Math.Max(0, p.Hp - BulletDmg);

                    BroadcastToRoom(new S_PLAYER_HIT
                    {
                        TargetPlayerId   = p.PlayerId,
                        AttackerPlayerId = b.OwnerId,
                        Damage           = BulletDmg,
                        RemainingHp      = p.Hp,
                    }.Serialize());

                    // 첫 번째 킬만 라운드 승자로 기록
                    if (p.Hp <= 0 && roundWinnerId < 0)
                        roundWinnerId = b.OwnerId;
                }
            }
            _bullets.RemoveAll(b => !b.IsAlive);

            // 7. 전체 상태 동기화
            BroadcastGameState();

            // 8. 라운드 종료 체크
            if (roundWinnerId >= 0)
                EndRound(roundWinnerId);
        }

        // ── 브로드캐스트 헬퍼 ─────────────────────────

        private void BroadcastToRoom(byte[] packet)
        {
            _hostSession.Send(packet);
            _guestSession?.Send(packet);
        }

        private void BroadcastRoomState()
        {
            BroadcastToRoom(new S_ROOM_STATE
            {
                RoomState     = Phase,
                HostPlayerId  = _hostPlayer.PlayerId,
                HostNickname  = _hostPlayer.Nickname,
                HostReady     = _hostPlayer.IsReady,
                GuestPlayerId = _guestPlayer?.PlayerId ?? -1,
                GuestNickname = _guestPlayer?.Nickname ?? string.Empty,
                GuestReady    = _guestPlayer?.IsReady ?? false,
                HostWins      = _hostWins,
                GuestWins     = _guestWins,
            }.Serialize());
        }

        private void BroadcastGameState()
        {
            if (_guestPlayer == null || _guestSession == null) return;

            long serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var playerData = new[]
            {
                new PlayerStateData { PlayerId = _hostPlayer.PlayerId,  PosX = _hostPlayer.PosX,  PosY = _hostPlayer.PosY,  Hp = _hostPlayer.Hp,  AimDirX = _hostPlayer.AimDirX,  AimDirY = _hostPlayer.AimDirY  },
                new PlayerStateData { PlayerId = _guestPlayer.PlayerId, PosX = _guestPlayer.PosX, PosY = _guestPlayer.PosY, Hp = _guestPlayer.Hp, AimDirX = _guestPlayer.AimDirX, AimDirY = _guestPlayer.AimDirY },
            };
            var bulletData = _bullets.ConvertAll(b => new BulletStateData
            {
                BulletId = b.BulletId, PosX = b.PosX, PosY = b.PosY, DirX = b.DirX, DirY = b.DirY,
            }).ToArray();

            // 각 클라이언트에 자신의 LastInputSeq를 담아 개별 전송
            _hostSession.Send(new S_GAME_STATE_SYNC
            {
                ServerTime = serverTime, LastProcessedInputSeq = _hostPlayer.LastInputSeq,
                Players = playerData, Bullets = bulletData,
            }.Serialize());
            _guestSession.Send(new S_GAME_STATE_SYNC
            {
                ServerTime = serverTime, LastProcessedInputSeq = _guestPlayer.LastInputSeq,
                Players = playerData, Bullets = bulletData,
            }.Serialize());
        }

        // ── 유틸리티 ─────────────────────────────────

        private static (float x, float y) ClampDir(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return (0, 0);
            float len = MathF.Sqrt(x * x + y * y);
            return len > 1f ? (x / len, y / len) : (x, y);
        }

        private static (float x, float y) NormalizeDir(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return (0, 0);
            float len = MathF.Sqrt(x * x + y * y);
            return len < 0.001f ? (0, 0) : (x / len, y / len);
        }

        public void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;
            _loopCts?.Cancel();
            _roomManager.OnPlayerLeft(_hostPlayer.PlayerId);
            if (_guestPlayer != null) _roomManager.OnPlayerLeft(_guestPlayer.PlayerId);
            _roomManager.OnRoomDestroyed(RoomId);
        }
    }
}
