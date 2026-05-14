using BedrockProtocol.Utils;
using BedrockProtocol.Types;

namespace BedrockProtocol.Packets
{
    /// <summary>
    /// Sent by the server each tick to confirm (or correct) the client's movement prediction.
    /// Without this packet, the client's ground-mode physics are throttled/frozen.
    /// </summary>
    public class CorrectPlayerMovePredictionPacket : Packet
    {
        public override uint PacketId => (uint)PacketIds.CorrectPlayerMovePrediction;

        /// <summary>0 = player, 1 = vehicle/mount</summary>
        public byte PredictionType { get; set; } = 0;

        public Vector3 Position { get; set; }
        public Vector3 Delta { get; set; }
        public bool OnGround { get; set; }
        public ulong Tick { get; set; }

        public override void Encode(BinaryStream stream)
        {
            stream.WriteByte(PredictionType);
            stream.WriteVector3(Position);
            stream.WriteVector3(Delta);
            stream.WriteBool(OnGround);
            stream.WriteUnsignedVarLong(Tick);
        }

        public override void Decode(BinaryStream stream)
        {
            PredictionType = stream.ReadByte();
            Position = stream.ReadVector3();
            Delta = stream.ReadVector3();
            OnGround = stream.ReadBool();
            Tick = stream.ReadUnsignedVarLong();
        }
    }
}
