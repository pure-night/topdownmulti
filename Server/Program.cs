using Server.Handler;
using Server.Network;
using Server.Room;

var sessionManager = new SessionManager();
var roomManager    = new RoomManager();
var dispatcher     = new PacketDispatcher();

LobbyHandler.Register(dispatcher, sessionManager, roomManager);
InGameHandler.Register(dispatcher, roomManager);

var server = new TcpServer(dispatcher, sessionManager);
await server.StartAsync("0.0.0.0", 7777);
