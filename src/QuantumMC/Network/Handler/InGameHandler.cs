using BedrockProtocol.Packets;
using BedrockProtocol.Packets.Enums;
using BedrockProtocol.Types;
using BedrockProtocol.Utils;
using QuantumMC.Utils;
using Serilog;

namespace QuantumMC.Network.Handler
{
    public class InGameHandler : PacketHandler
    {
        public override void Handle(PlayerSession session, uint packetId, byte[] payload)
        {
            switch ((PacketIds)packetId)
            {
                case PacketIds.Text:
                    HandleText(session, payload);
                    break;
                case PacketIds.MovePlayer:
                    HandleMovePlayer(session, payload);
                    break;
                case PacketIds.PlayerAuthInput:
                    HandlePlayerAuthInput(session, payload);
                    break;
                case PacketIds.Animate:
                    HandleAnimate(session, payload);
                    break;
                case PacketIds.CommandRequest:
                    HandleCommandRequest(session, payload);
                    break;
            }
        }

        // In Bedrock Edition, MovePlayerPacket.Position.Y is the eye/camera position
        // (feet + 1.62 for standing). We store feet Y to keep internal positions consistent.
        private const float EyeHeight = 1.62f;

        private void HandleMovePlayer(PlayerSession session, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new MovePlayerPacket();
            packet.Decode(stream);

            session.Player.X = packet.Position.X;
            session.Player.Y = packet.Position.Y - EyeHeight; // convert eye → feet
            session.Player.Z = packet.Position.Z;
            session.Player.Pitch = packet.Pitch;
            session.Player.Yaw = packet.Yaw;
            session.Player.HeadYaw = packet.HeadYaw;

            session.Player.UpdateChunks();

            var broadcastPacket = new MovePlayerPacket
            {
                RuntimeEntityId = session.Player.EntityRuntimeId,
                Position = packet.Position,
                Pitch = packet.Pitch,
                Yaw = packet.Yaw,
                HeadYaw = packet.HeadYaw,
                Mode = packet.Mode,
                OnGround = packet.OnGround,
                RidingEntityId = packet.RidingEntityId,
                Tick = packet.Tick
            };

            Server.Instance.Network.BroadcastPacket(broadcastPacket, true, session);
        }

        private void HandlePlayerAuthInput(PlayerSession session, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new PlayerAuthInputPacket();
            packet.Decode(stream);

            session.Player.X = packet.Position.X;
            session.Player.Y = packet.Position.Y - EyeHeight; // convert eye → feet
            session.Player.Z = packet.Position.Z;
            session.Player.Pitch = packet.Pitch;
            session.Player.Yaw = packet.Yaw;
            session.Player.HeadYaw = packet.HeadYaw;

            session.Player.UpdateChunks();

            // Confirm movement prediction so the client's ground-mode physics are not frozen.
            // Without this, the client waits for server confirmation and movement appears stuck/slow.
            var correction = new CorrectPlayerMovePredictionPacket
            {
                PredictionType = 0, // player (not vehicle)
                Position = packet.Position,
                Delta = packet.Delta,
                // onGround: true when no significant downward velocity (standing/jumping up)
                OnGround = packet.Delta.Y >= -0.05f,
                Tick = packet.Tick
            };
            session.SendPacket(correction);

            // Broadcast movement to other players using eye position (packet.Position)
            var broadcastPacket = new MovePlayerPacket
            {
                RuntimeEntityId = session.Player.EntityRuntimeId,
                Position = packet.Position,
                Pitch = packet.Pitch,
                Yaw = packet.Yaw,
                HeadYaw = packet.HeadYaw,
                Mode = MoveMode.Normal,
                OnGround = packet.Tick > 0,
                RidingEntityId = 0,
                Tick = packet.Tick
            };

            Server.Instance.Network.BroadcastPacket(broadcastPacket, true, session);
        }

        private void HandleText(PlayerSession session, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new TextPacket();
            packet.Decode(stream);

            Log.Information("{Username}: {Text}", session.Username, TextFormat.ToAnsi(packet.Message));
            Server.Instance.Network.BroadcastPacket(packet);
        }

        private void HandleAnimate(PlayerSession session, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new AnimatePacket();
            packet.Decode(stream);

            Server.Instance.Network.BroadcastPacket(packet, true, session);
        }

        private void HandleCommandRequest(PlayerSession session, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new CommandRequestPacket();
            packet.Decode(stream);

            Server.Instance.CommandMap.Dispatch(session.Player, packet.CommandLine);
        }
    }
}
