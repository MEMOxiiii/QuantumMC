namespace QuantumMC.Blocks
{
    public class BlockCommandBlock : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockCommandBlock() : base("minecraft:command_block") { }
    }
}
