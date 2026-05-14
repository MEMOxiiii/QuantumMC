namespace QuantumMC.Blocks
{
    public class BlockOakLog : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockOakLog() : base("minecraft:oak_log") { }
    }
}
