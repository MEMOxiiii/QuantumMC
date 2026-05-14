using System.Collections.Generic;
using BedrockProtocol.Packets.Enums;
using BedrockProtocol.Packets;
using Serilog;

namespace QuantumMC.Network.Handler
{
    public static class PacketDispatcher
    {
        private static readonly Dictionary<uint, PacketHandler> _handlers = new();

        static PacketDispatcher()
        {
            var loginHandler = new LoginHandler();
            var handshakeHandler = new HandshakeHandler();
            var resourcePackHandler = new ResourcePackHandler();
            var sessionHandler = new SessionStartPacketHandler();
            var playHandler = new PlayHandler();
            var inGameHandler = new InGameHandler();

            _handlers.Add((uint)PacketIds.Login, loginHandler);
            _handlers.Add((uint)PacketIds.ClientToServerHandshake, handshakeHandler);
            _handlers.Add((uint)PacketIds.ResourcePackClientResponse, resourcePackHandler);
            _handlers.Add((uint)PacketIds.RequestNetworkSettings, sessionHandler);
            _handlers.Add((uint)PacketIds.RequestChunkRadius, playHandler);
            _handlers.Add((uint)PacketIds.SetLocalPlayerAsInitialized, playHandler);
            _handlers.Add((uint)PacketIds.ServerBoundLoadingScreen, playHandler);
            _handlers.Add((uint)PacketIds.Text, inGameHandler);
            _handlers.Add((uint)PacketIds.MovePlayer, inGameHandler);
            _handlers.Add((uint)PacketIds.PlayerAuthInput, inGameHandler);
            _handlers.Add((uint)PacketIds.Animate, inGameHandler);
            _handlers.Add((uint)PacketIds.CommandRequest, inGameHandler);
        }

        public static void Dispatch(PlayerSession session, uint packetId, byte[] payload)
        {
            Log.Information("[PKT] RX 0x{PacketId:X2} ({PacketName}) from {Username} (state={State})",
                packetId, (PacketIds)packetId, session.Username, session.State);

            if (_handlers.TryGetValue(packetId, out var handler))
            {
                try
                {
                    handler.Handle(session, packetId, payload);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[PKT] Exception handling 0x{PacketId:X2} for {Username}", packetId, session.Username);
                }
            }
            else
            {
                Log.Debug("[PKT] Unhandled 0x{PacketId:X2} ({PacketName}) from {EndPoint}", packetId, (PacketIds)packetId, session.EndPoint);
            }
        }
    }
}
