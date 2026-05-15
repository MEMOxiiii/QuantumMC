using BedrockProtocol.Utils;

namespace BedrockProtocol.Packets
{
    /// <summary>
    /// Required by Bedrock clients since MC 1.20.10.
    /// Must be sent during the join sequence before PlayStatus(PlayerSpawn).
    /// Empty pattern/material lists are valid for servers with no custom trims.
    /// </summary>
    public class TrimDataPacket : Packet
    {
        public override uint PacketId => (uint)PacketIds.TrimData;

        public override void Encode(BinaryStream stream)
        {
            stream.WriteUnsignedVarInt(0); // trim patterns count
            stream.WriteUnsignedVarInt(0); // trim materials count
        }

        public override void Decode(BinaryStream stream) { }
    }
}
