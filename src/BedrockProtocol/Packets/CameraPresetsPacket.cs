using BedrockProtocol.Utils;
using System.Collections.Generic;

namespace BedrockProtocol.Packets
{
    /// <summary>
    /// Required by Bedrock clients since protocol ~671 (MC 1.20.60).
    /// Must be sent during the join sequence before PlayStatus(PlayerSpawn).
    /// An empty preset list is valid and tells the client to use only built-in camera presets.
    /// </summary>
    public class CameraPresetsPacket : Packet
    {
        public override uint PacketId => (uint)PacketIds.CameraPresets;

        // No custom presets — client uses built-in camera presets only.
        public override void Encode(BinaryStream stream)
        {
            stream.WriteUnsignedVarInt(0); // presets count
        }

        public override void Decode(BinaryStream stream) { }
    }
}
