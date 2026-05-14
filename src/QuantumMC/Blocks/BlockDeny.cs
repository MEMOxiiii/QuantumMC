namespace QuantumMC.Blocks
{
    public class BlockDeny : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockDeny() : base("minecraft:deny") { }
    }
}
