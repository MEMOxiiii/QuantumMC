using BedrockProtocol.Packets;
using BedrockProtocol.Packets.Types;
using BedrockProtocol.Utils;
using QuantumMC.Utils;
using QuantumMC.Event.Impl;
using Serilog;

namespace QuantumMC.Network.Handler
{
    public class PlayHandler : PacketHandler
    {
        public override void Handle(PlayerSession session, uint packetId, byte[] payload)
        {
            switch ((PacketIds)packetId)
            {
                case PacketIds.RequestChunkRadius:
                    HandleRequestChunkRadius(session, payload);
                    break;

                case PacketIds.SetLocalPlayerAsInitialized:
                    HandleSetLocalPlayerAsInitialized(session, payload);
                    break;

                case PacketIds.ServerBoundLoadingScreen:
                    HandleServerBoundLoadingScreen(session, payload);
                    break;
            }
        }

        private void HandleRequestChunkRadius(PlayerSession session, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new RequestChunkRadiusPacket();
            packet.Decode(stream);

            Log.Debug("Player {Username} requested chunk radius: {Radius} (max: {MaxRadius})",
                session.Username, packet.Radius, packet.MaxRadius);

            if (session.Player.World == null)
            {
                Log.Error("World is null for player {Username}, cannot send chunks", session.Username);
                return;
            }

            int grantedRadius = Math.Min(packet.Radius, session.Player.World.MaxChunkRadius);
            session.Player.ChunkRadius = grantedRadius;

            Log.Information("[JOIN] Sending chunks radius={Radius} to {Username}", grantedRadius, session.Username);

            var radiusResponse = new ChunkRadiusUpdatedPacket
            {
                Radius = grantedRadius
            };
            session.SendPacket(radiusResponse);

            session.Player.UpdateChunks();
            Log.Information("[JOIN] Chunks sent, sending PlayerSpawn to {Username}", session.Username);

            var spawnStatus = new PlayStatusPacket
            {
                Status = BedrockProtocol.Packets.Enums.PlayStatus.PlayerSpawn
            };
            session.SendPacket(spawnStatus);

            var playerJoinEvent = new PlayerJoinEvent(session.Player, "%multiplayer.player.joined");
            Server.Instance.PluginManager.EventManager.CallEventAsync(playerJoinEvent).GetAwaiter().GetResult();

            if (playerJoinEvent.JoinMessage != null)
            {
                Server.Instance.SendTranslation(TextFormat.Yellow + playerJoinEvent.JoinMessage, [session.Username]);
            }
        }

        private void HandleSetLocalPlayerAsInitialized(PlayerSession session, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new SetLocalPlayerAsInitializedPacket();
            packet.Decode(stream);

            session.State = SessionState.InGamePhase;
            session.Player.SendCommandData();
        }

        private void HandleServerBoundLoadingScreen(PlayerSession session, byte[] payload)
        {
            // Protocol 975 (MC 1.26.20): packet fields are
            //   [0] Loading Screen Packet Type (VarInt zigzag-encoded int32):
            //       wire 0 = StartLoadingScreen, wire 2 = EndLoadingScreen (decoded value 1)
            //   [1] Loading Screen Id (uint32 LE): always 0 for initial world load.
            //
            // The server must NOT respond to either packet type for initial world load.
            // Sending PlayStatus(PlayerSpawn) again here causes the client to disconnect
            // immediately because it receives a duplicate spawn signal while already loaded.
            // After EndLoadingScreen the client will send SetLocalPlayerAsInitialized on its own.
            var stream = new BinaryStream(payload);
            uint type = stream.ReadUnsignedVarInt();
            string typeName = type == 0 ? "StartLoadingScreen" : type == 2 ? "EndLoadingScreen" : $"Unknown({type})";
            Log.Information("[JOIN] ServerboundLoadingScreen {TypeName} (raw={Type}) from {Username}", typeName, type, session.Username);
        }
    }
}
